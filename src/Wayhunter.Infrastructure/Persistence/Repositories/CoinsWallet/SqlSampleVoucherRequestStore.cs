using System.Data;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlSampleVoucherRequestStore
    : ISampleVoucherRequestStore
{
    private readonly WayhunterSharedDbContext _db;
    private readonly IVoucherSecretProtector _protector;

    public SqlSampleVoucherRequestStore(
        WayhunterSharedDbContext db,
        IVoucherSecretProtector protector)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _protector = protector ??
            throw new ArgumentNullException(nameof(protector));
    }

    public async Task<IReadOnlyList<SampleVoucherOfferResponse>>
        GetAvailableOffersAsync(
            string ownerUserId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
    {
        string ownerId = RequireOwner(ownerUserId);
        RequireUtc(nowUtc, nameof(nowUtc));

        long spendableCoins = await _db.CoinWallets
            .AsNoTracking()
            .Where(x => x.UserId == ownerId)
            .Select(x => x.PostedCoins - x.ReservedCoins)
            .SingleOrDefaultAsync(cancellationToken);

        var offers = await _db.SampleVoucherOffers
            .AsNoTracking()
            .Where(x => x.IsActive &&
                        x.ValidFromUtc <= nowUtc &&
                        x.ValidUntilUtc > nowUtc)
            .Select(x => new
            {
                x.Id,
                x.OfferCode,
                x.CostCoins,
                x.ReferenceValueLkr,
                x.ValidUntilUtc,
                AvailableStock = _db.SampleVoucherInventory.Count(i =>
                    i.OfferId == x.Id &&
                    i.AssignedRequestId == null &&
                    i.ExpiresAtUtc > nowUtc)
            })
            .Where(x => x.AvailableStock > 0)
            .OrderBy(x => x.OfferCode)
            .ToListAsync(cancellationToken);

        bool eligible = spendableCoins >=
            CoinLevelRedemptionRules
                .MinimumSpendableForRequestCoins;

        return offers.Select(x =>
            new SampleVoucherOfferResponse(
                x.Id,
                x.OfferCode,
                x.CostCoins,
                x.ReferenceValueLkr,
                x.ValidUntilUtc,
                x.AvailableStock,
                eligible &&
                x.CostCoins == SampleVoucherPolicy.VoucherCostCoins))
            .ToArray();
    }

    public async Task<SampleVoucherRequestResponse> RequestAsync(
        string ownerUserId,
        CreateSampleVoucherRequest request,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        string ownerId = RequireOwner(ownerUserId);
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(requestedAtUtc, nameof(requestedAtUtc));

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            await SqlServerTransactionLock.AcquireAsync(
                _db,
                "voucher-request",
                request.RequestId.ToString("D"),
                cancellationToken);

            await SqlServerTransactionLock.AcquireAsync(
                _db,
                "wallet",
                ownerId,
                cancellationToken);

            var existing = await _db
                .SampleVoucherRedemptionRequests
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == request.RequestId,
                    cancellationToken);

            if (existing is not null)
            {
                if (!string.Equals(
                        existing.OwnerUserId,
                        ownerId,
                        StringComparison.Ordinal) ||
                    existing.OfferId != request.OfferId)
                {
                    throw new InvalidOperationException(
                        "Request ID is already used for another request.");
                }

                await transaction.RollbackAsync(cancellationToken);
                return Map(existing);
            }

            var offer = await _db.SampleVoucherOffers
                .SingleOrDefaultAsync(
                    x => x.Id == request.OfferId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Voucher offer was not found.");

            if (!offer.IsActive ||
                offer.ValidFromUtc > requestedAtUtc ||
                offer.ValidUntilUtc <= requestedAtUtc)
            {
                throw new InvalidOperationException(
                    "Voucher offer is not currently available.");
            }

            if (offer.CostCoins !=
                    SampleVoucherPolicy.VoucherCostCoins ||
                offer.ReferenceValueLkr !=
                    SampleVoucherPolicy.ReferenceValueLkr)
            {
                throw new InvalidOperationException(
                    "Voucher offer does not match the approved demo policy.");
            }

            bool hasStock = await _db.SampleVoucherInventory
                .AnyAsync(
                    x => x.OfferId == offer.Id &&
                         x.AssignedRequestId == null &&
                         x.ExpiresAtUtc > requestedAtUtc,
                    cancellationToken);

            if (!hasStock)
                throw new InvalidOperationException(
                    "Voucher offer has no available stock.");

            var wallet = await _db.CoinWallets
                .SingleOrDefaultAsync(
                    x => x.UserId == ownerId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Coins wallet was not found.");

            long spendable = checked(
                wallet.PostedCoins - wallet.ReservedCoins);

            if (spendable < CoinLevelRedemptionRules
                    .MinimumSpendableForRequestCoins)
            {
                throw new InvalidOperationException(
                    "At least 10,100 spendable Coins are required.");
            }

            string ownerName = await _db.Users
                .AsNoTracking()
                .Where(x => x.UserId == ownerId)
                .Select(x => x.DisplayName)
                .SingleAsync(cancellationToken);

            var row = new SampleVoucherRedemptionRequestRow
            {
                Id = request.RequestId,
                WalletId = wallet.Id,
                OfferId = offer.Id,
                OwnerUserId = ownerId,
                OwnerNameSnapshot = ownerName,
                CostCoins = SampleVoucherPolicy.VoucherCostCoins,
                Status = SampleVoucherRequestStatusDraft.Pending,
                RequestedAtUtc = requestedAtUtc
            };

            var reservation = new CoinReservationRow
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                RequestId = row.Id,
                ReservedCoins = SampleVoucherPolicy.VoucherCostCoins,
                Status = CoinReservationState.Pending,
                CreatedAtUtc = requestedAtUtc
            };

            wallet.ReservedCoins = checked(
                wallet.ReservedCoins +
                SampleVoucherPolicy.VoucherCostCoins);

            _db.SampleVoucherRedemptionRequests.Add(row);
            _db.CoinReservations.Add(reservation);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Map(row);
        });
    }

    public async Task<SampleVoucherRequestResponse?>
        GetOwnedRequestAsync(
            string ownerUserId,
            Guid requestId,
            CancellationToken cancellationToken = default)
    {
        string ownerId = RequireOwner(ownerUserId);

        var row = await _db.SampleVoucherRedemptionRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == requestId &&
                     x.OwnerUserId == ownerId,
                cancellationToken);

        return row is null ? null : Map(row);
    }

    public async Task<SampleVoucherRequestResponse> CancelAsync(
        string ownerUserId,
        Guid requestId,
        string reason,
        DateTimeOffset cancelledAtUtc,
        CancellationToken cancellationToken = default)
    {
        string ownerId = RequireOwner(ownerUserId);
        RequireUtc(cancelledAtUtc, nameof(cancelledAtUtc));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.");

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            await SqlServerTransactionLock.AcquireAsync(
                _db,
                "voucher-request",
                requestId.ToString("D"),
                cancellationToken);
            await SqlServerTransactionLock.AcquireAsync(
                _db,
                "wallet",
                ownerId,
                cancellationToken);

            var row = await _db.SampleVoucherRedemptionRequests
                .SingleOrDefaultAsync(
                    x => x.Id == requestId &&
                         x.OwnerUserId == ownerId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Voucher request was not found.");

            if (row.Status ==
                SampleVoucherRequestStatusDraft.Cancelled)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Map(row);
            }

            if (row.Status != SampleVoucherRequestStatusDraft.Pending)
                throw new InvalidOperationException(
                    "Only a pending request can be cancelled.");

            var wallet = await _db.CoinWallets
                .SingleAsync(
                    x => x.Id == row.WalletId,
                    cancellationToken);

            var reservation = await _db.CoinReservations
                .SingleAsync(
                    x => x.RequestId == row.Id,
                    cancellationToken);

            if (reservation.Status != CoinReservationState.Pending)
                throw new InvalidOperationException(
                    "The pending reservation is not active.");

            wallet.ReservedCoins = checked(
                wallet.ReservedCoins - reservation.ReservedCoins);
            reservation.Status = CoinReservationState.Released;
            reservation.ResolvedAtUtc = cancelledAtUtc;

            row.Status = SampleVoucherRequestStatusDraft.Cancelled;
            row.DecisionActorUserId = ownerId;
            row.DecisionReason = reason.Trim();
            row.DecidedAtUtc = cancelledAtUtc;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(row);
        });
    }

    public async Task<MyIssuedSampleVoucherResponse?>
        GetOwnedVoucherAsync(
            string ownerUserId,
            Guid requestId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
    {
        string ownerId = RequireOwner(ownerUserId);
        RequireUtc(nowUtc, nameof(nowUtc));

        var result = await (
            from request in _db.SampleVoucherRedemptionRequests
                .AsNoTracking()
            join offer in _db.SampleVoucherOffers.AsNoTracking()
                on request.OfferId equals offer.Id
            join issuance in _db.SampleVoucherIssuances.AsNoTracking()
                on request.Id equals issuance.RequestId
            join inventory in _db.SampleVoucherInventory.AsNoTracking()
                on issuance.InventoryId equals (Guid?)inventory.Id
            where request.Id == requestId &&
                  request.OwnerUserId == ownerId
            select new
            {
                request.Id,
                offer.ReferenceValueLkr,
                issuance.Status,
                issuance.QrPayload,
                issuance.ImageReference,
                issuance.IssuedAtUtc,
                issuance.ExpiresAtUtc,
                issuance.UsedAtUtc,
                inventory.EncryptedCodeReference
            }).SingleOrDefaultAsync(cancellationToken);

        if (result is null)
            return null;

        var status = result.Status;
        if (status == SampleVoucherIssueStatusDraft.Issued &&
            result.ExpiresAtUtc <= nowUtc)
        {
            status = SampleVoucherIssueStatusDraft.Expired;
        }

        bool reveal = status is
            SampleVoucherIssueStatusDraft.Issued or
            SampleVoucherIssueStatusDraft.Used;

        return new MyIssuedSampleVoucherResponse(
            result.Id,
            status,
            result.ReferenceValueLkr,
            reveal
                ? _protector.Unprotect(
                    result.EncryptedCodeReference)
                : null,
            reveal ? result.QrPayload : null,
            reveal ? result.ImageReference : null,
            result.IssuedAtUtc,
            result.ExpiresAtUtc,
            result.UsedAtUtc);
    }

    private static SampleVoucherRequestResponse Map(
        SampleVoucherRedemptionRequestRow row)
    {
        return new SampleVoucherRequestResponse(
            row.Id,
            row.OfferId,
            row.OwnerUserId,
            row.OwnerNameSnapshot,
            row.CostCoins,
            row.Status,
            row.RequestedAtUtc,
            row.DecidedAtUtc,
            row.DecisionReason);
    }

    private static string RequireOwner(string ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user ID is required.");

        return ownerUserId.Trim();
    }

    private static void RequireUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "Valid UTC time is required.",
                parameterName);
    }
}
