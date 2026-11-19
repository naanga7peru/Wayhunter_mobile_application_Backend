using System.Data;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlSampleVoucherAdminStore
    : ISampleVoucherAdminStore
{
    private readonly WayhunterSharedDbContext _db;
    private readonly IVoucherSecretProtector _protector;

    public SqlSampleVoucherAdminStore(
        WayhunterSharedDbContext db,
        IVoucherSecretProtector protector)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _protector = protector ??
            throw new ArgumentNullException(nameof(protector));
    }

    public async Task CreateOfferAsync(
        string adminUserId,
        CreateSampleVoucherOfferRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActor(adminUserId);
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(request.ValidFromUtc, nameof(request.ValidFromUtc));
        RequireUtc(request.ValidUntilUtc, nameof(request.ValidUntilUtc));

        if (request.OfferId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.OfferCode) ||
            request.ValidUntilUtc <= request.ValidFromUtc)
        {
            throw new ArgumentException("Invalid voucher offer.");
        }

        var existing = await _db.SampleVoucherOffers
            .SingleOrDefaultAsync(
                x => x.Id == request.OfferId,
                cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(
                    existing.OfferCode,
                    request.OfferCode.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Offer ID is already in use.");
            }

            return;
        }

        _db.SampleVoucherOffers.Add(new SampleVoucherOfferRow
        {
            Id = request.OfferId,
            OfferCode = request.OfferCode.Trim(),
            CostCoins = SampleVoucherPolicy.VoucherCostCoins,
            ReferenceValueLkr = SampleVoucherPolicy.ReferenceValueLkr,
            IsActive = true,
            ValidFromUtc = request.ValidFromUtc,
            ValidUntilUtc = request.ValidUntilUtc
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SampleVoucherInventoryResponse>
        AddInventoryAsync(
            string adminUserId,
            AddSampleVoucherInventoryRequest request,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
    {
        string actor = RequireActor(adminUserId);
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc, nameof(nowUtc));
        RequireUtc(request.ExpiresAtUtc, nameof(request.ExpiresAtUtc));

        if (request.InventoryId == Guid.Empty ||
            request.OfferId == Guid.Empty ||
            request.ExpiresAtUtc <= nowUtc ||
            string.IsNullOrWhiteSpace(request.SampleImageReference))
        {
            throw new ArgumentException("Invalid voucher inventory item.");
        }

        if (Uri.TryCreate(
                request.SampleImageReference.Trim(),
                UriKind.Absolute,
                out var mediaUri) &&
            mediaUri.Scheme is "http" or "https")
        {
            throw new ArgumentException(
                "Voucher image must use a protected media reference.");
        }

        var offer = await _db.SampleVoucherOffers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.OfferId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Voucher offer was not found.");

        if (request.ExpiresAtUtc > offer.ValidUntilUtc)
            throw new InvalidOperationException(
                "Inventory expiry exceeds the offer validity window.");

        string codeHash = _protector.ComputeHash(request.VoucherCode);
        string protectedCode = _protector.Protect(request.VoucherCode);

        var existing = await _db.SampleVoucherInventory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.InventoryId,
                cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(
                    existing.CodeHash,
                    codeHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Inventory ID is already in use.");
            }

            return MapInventory(existing);
        }

        var row = new SampleVoucherInventoryRow
        {
            Id = request.InventoryId,
            OfferId = request.OfferId,
            CodeHash = codeHash,
            EncryptedCodeReference = protectedCode,
            SampleImageReference = request.SampleImageReference.Trim(),
            ExpiresAtUtc = request.ExpiresAtUtc,
            AddedByAdminUserId = actor,
            AddedAtUtc = nowUtc
        };

        _db.SampleVoucherInventory.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return MapInventory(row);
    }

    public async Task<SampleVoucherAdminQueueResponse> GetQueueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            throw new ArgumentOutOfRangeException(nameof(page));

        var query = _db.SampleVoucherRedemptionRequests
            .AsNoTracking()
            .Where(x =>
                x.Status == SampleVoucherRequestStatusDraft.Pending);

        int totalCount = await query.CountAsync(cancellationToken);
        int skip = checked((page - 1) * pageSize);

        var items = await query
            .OrderBy(x => x.RequestedAtUtc)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new SampleVoucherAdminQueueItem(
                x.Id,
                x.OfferId,
                x.OwnerUserId,
                x.OwnerNameSnapshot,
                x.CostCoins,
                x.RequestedAtUtc,
                _db.SampleVoucherInventory.Count(i =>
                    i.OfferId == x.OfferId &&
                    i.AssignedRequestId == null &&
                    i.ExpiresAtUtc > DateTimeOffset.UtcNow)))
            .ToListAsync(cancellationToken);

        return new SampleVoucherAdminQueueResponse(
            page,
            pageSize,
            totalCount,
            items);
    }

    public Task<SampleVoucherRequestResponse> ApproveAsync(
        string adminUserId,
        Guid requestId,
        SampleVoucherDecisionRequest decision,
        DateTimeOffset decidedAtUtc,
        string ruleVersion,
        CancellationToken cancellationToken = default)
    {
        string actor = RequireActor(adminUserId);
        ValidateDecision(requestId, decision, decidedAtUtc);
        string normalizedRule =
            CoinLedgerVersionPolicy.RequireRuleVersion(ruleVersion);

        return ExecuteDecisionAsync(
            actor,
            requestId,
            decision,
            decidedAtUtc,
            approve: true,
            normalizedRule,
            cancellationToken);
    }

    public Task<SampleVoucherRequestResponse> RejectAsync(
        string adminUserId,
        Guid requestId,
        SampleVoucherDecisionRequest decision,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default)
    {
        string actor = RequireActor(adminUserId);
        ValidateDecision(requestId, decision, decidedAtUtc);

        return ExecuteDecisionAsync(
            actor,
            requestId,
            decision,
            decidedAtUtc,
            approve: false,
            ruleVersion: string.Empty,
            cancellationToken);
    }

    private async Task<SampleVoucherRequestResponse>
        ExecuteDecisionAsync(
            string actor,
            Guid requestId,
            SampleVoucherDecisionRequest decision,
            DateTimeOffset decidedAtUtc,
            bool approve,
            string ruleVersion,
            CancellationToken cancellationToken)
    {
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

            var request = await _db.SampleVoucherRedemptionRequests
                .SingleOrDefaultAsync(
                    x => x.Id == requestId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Voucher request was not found.");

            var expectedStatus = approve
                ? SampleVoucherRequestStatusDraft.Approved
                : SampleVoucherRequestStatusDraft.Rejected;

            if (request.Status == expectedStatus)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MapRequest(request);
            }

            if (request.Status != SampleVoucherRequestStatusDraft.Pending)
                throw new InvalidOperationException(
                    "A conflicting voucher decision already exists.");

            await SqlServerTransactionLock.AcquireAsync(
                _db,
                "wallet",
                request.OwnerUserId,
                cancellationToken);

            if (approve)
            {
                await SqlServerTransactionLock.AcquireAsync(
                    _db,
                    "voucher-stock",
                    request.OfferId.ToString("D"),
                    cancellationToken);
            }

            var wallet = await _db.CoinWallets
                .SingleAsync(
                    x => x.Id == request.WalletId,
                    cancellationToken);
            var reservation = await _db.CoinReservations
                .SingleAsync(
                    x => x.RequestId == request.Id,
                    cancellationToken);

            if (reservation.Status != CoinReservationState.Pending ||
                reservation.ReservedCoins != request.CostCoins)
            {
                throw new InvalidOperationException(
                    "The request has no valid active reservation.");
            }

            if (approve)
            {
                var inventory = await _db.SampleVoucherInventory
                    .Where(x => x.OfferId == request.OfferId &&
                                x.AssignedRequestId == null &&
                                x.ExpiresAtUtc > decidedAtUtc)
                    .OrderBy(x => x.ExpiresAtUtc)
                    .ThenBy(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "No valid voucher stock is available.");

                inventory.AssignedRequestId = request.Id;

                wallet.PostedCoins = checked(
                    wallet.PostedCoins - request.CostCoins);
                wallet.ReservedCoins = checked(
                    wallet.ReservedCoins - request.CostCoins);
                reservation.Status = CoinReservationState.Consumed;
                reservation.ResolvedAtUtc = decidedAtUtc;

                _db.CoinLedgerEntries.Add(new CoinLedgerEntryRow
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    DeltaCoins = -request.CostCoins,
                    SourceRecordId =
                        $"voucher-debit:{request.Id:D}",
                    RuleVersion = ruleVersion,
                    CorrelationId = decision.DecisionId,
                    RecordedAtUtc = decidedAtUtc
                });

                request.Status =
                    SampleVoucherRequestStatusDraft.Approved;
            }
            else
            {
                wallet.ReservedCoins = checked(
                    wallet.ReservedCoins - request.CostCoins);
                reservation.Status = CoinReservationState.Released;
                reservation.ResolvedAtUtc = decidedAtUtc;
                request.Status =
                    SampleVoucherRequestStatusDraft.Rejected;
            }

            request.AdminDecisionId = decision.DecisionId;
            request.DecisionActorUserId = actor;
            request.DecisionReason = decision.Reason.Trim();
            request.DecidedAtUtc = decidedAtUtc;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MapRequest(request);
        });
    }

    public async Task<SampleVoucherIssuanceResponse> IssueAsync(
        string adminUserId,
        Guid requestId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActor(adminUserId);
        RequireUtc(nowUtc, nameof(nowUtc));

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

            var request = await _db.SampleVoucherRedemptionRequests
                .SingleOrDefaultAsync(
                    x => x.Id == requestId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Voucher request was not found.");

            if (request.Status !=
                SampleVoucherRequestStatusDraft.Approved)
            {
                throw new InvalidOperationException(
                    "Only an approved request can be issued.");
            }

            bool debitExists = await _db.CoinLedgerEntries
                .AnyAsync(
                    x => x.WalletId == request.WalletId &&
                         x.SourceRecordId ==
                            $"voucher-debit:{request.Id:D}" &&
                         x.DeltaCoins == -request.CostCoins,
                    cancellationToken);

            if (!debitExists)
                throw new InvalidOperationException(
                    "Approved debit is not committed.");

            var inventory = await _db.SampleVoucherInventory
                .SingleOrDefaultAsync(
                    x => x.AssignedRequestId == request.Id,
                    cancellationToken);

            var issuance = await _db.SampleVoucherIssuances
                .SingleOrDefaultAsync(
                    x => x.RequestId == request.Id,
                    cancellationToken);

            if (issuance?.Status is
                SampleVoucherIssueStatusDraft.Issued or
                SampleVoucherIssueStatusDraft.Used or
                SampleVoucherIssueStatusDraft.Expired)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MapIssuance(issuance);
            }

            issuance ??= new SampleVoucherIssuanceRow
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                Status = SampleVoucherIssueStatusDraft.Unissued
            };

            if (_db.Entry(issuance).State == EntityState.Detached)
                _db.SampleVoucherIssuances.Add(issuance);

            issuance.AttemptCount = checked(
                issuance.AttemptCount + 1);

            if (inventory is null || inventory.ExpiresAtUtc <= nowUtc)
            {
                issuance.Status = SampleVoucherIssueStatusDraft.Failed;
                issuance.LastErrorCode = "VOUCHER_STOCK_UNAVAILABLE";
                issuance.InventoryId = inventory?.Id;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return MapIssuance(issuance);
            }

            // Validate that the protected code can be recovered before
            // marking the voucher issued. The plaintext is never logged
            // or persisted outside its protected inventory field.
            _ = _protector.Unprotect(
                inventory.EncryptedCodeReference);

            issuance.InventoryId = inventory.Id;
            issuance.Status = SampleVoucherIssueStatusDraft.Issued;
            issuance.QrPayload =
                $"WAYHUNTER-DEMO:{request.Id:D}:{inventory.CodeHash}";
            issuance.ImageReference = inventory.SampleImageReference;
            issuance.IssuedAtUtc = nowUtc;
            issuance.ExpiresAtUtc = inventory.ExpiresAtUtc;
            issuance.LastErrorCode = null;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MapIssuance(issuance);
        });
    }

    public Task<SampleVoucherIssuanceResponse> ConsumeAsync(
        string adminUserId,
        Guid requestId,
        ConsumeSampleVoucherRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        string actor = RequireActor(adminUserId);
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc, nameof(nowUtc));
        if (requestId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length > 1000)
            throw new ArgumentException("Invalid voucher use confirmation.");

        return _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
            await SqlServerTransactionLock.AcquireAsync(
                _db, "voucher-request", requestId.ToString("D"), cancellationToken);
            var issuance = await _db.SampleVoucherIssuances.SingleOrDefaultAsync(
                x => x.RequestId == requestId, cancellationToken)
                ?? throw new InvalidOperationException("Issued voucher was not found.");

            if (issuance.Status == SampleVoucherIssueStatusDraft.Used)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MapIssuance(issuance);
            }
            if (issuance.Status != SampleVoucherIssueStatusDraft.Issued)
                throw new InvalidOperationException("Voucher is not available for use.");
            if (!issuance.ExpiresAtUtc.HasValue || issuance.ExpiresAtUtc <= nowUtc)
            {
                issuance.Status = SampleVoucherIssueStatusDraft.Expired;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return MapIssuance(issuance);
            }

            issuance.Status = SampleVoucherIssueStatusDraft.Used;
            issuance.UsedAtUtc = nowUtc;
            issuance.UsedByActorUserId = actor;
            issuance.UseReason = request.Reason.Trim();
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MapIssuance(issuance);
        });
    }

    public async Task<int> ExpireDueAsync(
        DateTimeOffset nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (maxCount < 1 || maxCount > 1000)
            throw new ArgumentOutOfRangeException(nameof(maxCount));

        var ids = await _db.SampleVoucherIssuances.AsNoTracking()
            .Where(x => x.Status == SampleVoucherIssueStatusDraft.Issued &&
                        x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .Select(x => x.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
        if (ids.Count == 0)
            return 0;
        return await _db.SampleVoucherIssuances
            .Where(x => ids.Contains(x.Id) &&
                        x.Status == SampleVoucherIssueStatusDraft.Issued &&
                        x.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(
                x => x.SetProperty(
                    y => y.Status,
                    SampleVoucherIssueStatusDraft.Expired),
                cancellationToken);
    }

    private static void ValidateDecision(
        Guid requestId,
        SampleVoucherDecisionRequest decision,
        DateTimeOffset decidedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(decision);
        RequireUtc(decidedAtUtc, nameof(decidedAtUtc));

        if (requestId == Guid.Empty ||
            decision.DecisionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(decision.Reason) ||
            decision.Reason.Trim().Length > 1000)
        {
            throw new ArgumentException("Invalid voucher decision.");
        }
    }

    private static SampleVoucherInventoryResponse MapInventory(
        SampleVoucherInventoryRow row) => new(
            row.Id,
            row.OfferId,
            row.ExpiresAtUtc,
            row.AssignedRequestId.HasValue,
            row.AssignedRequestId,
            row.SampleImageReference);

    private static SampleVoucherRequestResponse MapRequest(
        SampleVoucherRedemptionRequestRow row) => new(
            row.Id,
            row.OfferId,
            row.OwnerUserId,
            row.OwnerNameSnapshot,
            row.CostCoins,
            row.Status,
            row.RequestedAtUtc,
            row.DecidedAtUtc,
            row.DecisionReason);

    private static SampleVoucherIssuanceResponse MapIssuance(
        SampleVoucherIssuanceRow row) => new(
            row.RequestId,
            row.Status,
            row.IssuedAtUtc,
            row.ExpiresAtUtc,
            row.AttemptCount,
            row.LastErrorCode);

    private static string RequireActor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new UnauthorizedAccessException(
                "Trusted actor identity is required.");

        return value.Trim();
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
