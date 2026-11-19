using System.Data;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlCoinAdministrationStore : ICoinAdministrationStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlCoinAdministrationStore(WayhunterSharedDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<CoinLedgerCorrectionResponse> ReverseLedgerEntryAsync(
        string actorUserId, Guid ledgerEntryId,
        ReverseCoinLedgerEntryRequest request, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        string actor = RequireActor(actorUserId);
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc);
        if (ledgerEntryId == Guid.Empty || request.CorrectionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length > 1000 ||
            string.IsNullOrWhiteSpace(request.RuleVersion))
            throw new ArgumentException("Invalid ledger correction.");

        return _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await SqlServerTransactionLock.AcquireAsync(
                _db, "coin-ledger", ledgerEntryId.ToString("D"), cancellationToken);

            var original = await _db.CoinLedgerEntries.SingleOrDefaultAsync(
                x => x.Id == ledgerEntryId, cancellationToken)
                ?? throw new InvalidOperationException("Ledger entry was not found.");
            if (original.SourceRecordId.StartsWith(
                    "voucher-debit:", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Voucher debit refunds are not an approved Coins rule.");

            await SqlServerTransactionLock.AcquireAsync(
                _db, "wallet-id", original.WalletId.ToString("D"), cancellationToken);
            var wallet = await _db.CoinWallets.SingleAsync(
                x => x.Id == original.WalletId, cancellationToken);
            var prior = await _db.CoinLedgerEntries.SingleOrDefaultAsync(
                x => x.Id == request.CorrectionId ||
                     x.CorrectionOfEntryId == ledgerEntryId,
                cancellationToken);
            if (prior is not null)
            {
                if (prior.Id != request.CorrectionId)
                    throw new InvalidOperationException(
                        "The ledger entry was already corrected.");
                await tx.RollbackAsync(cancellationToken);
                return MapCorrection(prior, original, wallet);
            }

            long delta = checked(-original.DeltaCoins);
            long posted = checked(wallet.PostedCoins + delta);
            long lifetime = original.RewardType.HasValue && original.DeltaCoins > 0
                ? checked(wallet.LifetimeEarnedCoins - original.DeltaCoins)
                : wallet.LifetimeEarnedCoins;
            if (posted < 0 || lifetime < 0 || wallet.ReservedCoins > posted)
                throw new InvalidOperationException(
                    "Correction would violate wallet balance invariants.");

            var correction = new CoinLedgerEntryRow
            {
                Id = request.CorrectionId,
                WalletId = wallet.Id,
                DeltaCoins = delta,
                SourceRecordId = $"correction:{ledgerEntryId:D}",
                RuleVersion = request.RuleVersion.Trim(),
                CorrelationId = request.CorrectionId,
                RecordedAtUtc = nowUtc,
                CorrectionOfEntryId = ledgerEntryId,
                ActorUserId = actor,
                Reason = request.Reason.Trim()
            };
            wallet.PostedCoins = posted;
            wallet.LifetimeEarnedCoins = lifetime;
            _db.CoinLedgerEntries.Add(correction);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return MapCorrection(correction, original, wallet);
        });
    }

    public async Task<CoinRuleVersionResponse> CreateRuleVersionAsync(
        string actorUserId, CreateCoinRuleVersionRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        string actor = RequireActor(actorUserId);
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc);
        RequireUtc(request.EffectiveFromUtc);
        if (request.RuleVersionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.VersionCode) ||
            request.VersionCode.Trim().Length > 64 ||
            request.EffectiveUntilUtc is { } until &&
            (until.Offset != TimeSpan.Zero || until <= request.EffectiveFromUtc))
            throw new ArgumentException("Invalid rule version.");

        var existing = await _db.CoinRuleVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.RuleVersionId ||
                x.VersionCode == request.VersionCode.Trim(), cancellationToken);
        if (existing is not null)
        {
            if (existing.Id != request.RuleVersionId)
                throw new InvalidOperationException("Rule version already exists.");
            return MapRule(existing);
        }

        var row = new CoinRuleVersionRow
        {
            Id = request.RuleVersionId,
            VersionCode = request.VersionCode.Trim(),
            VoucherCostCoins = SampleVoucherPolicy.VoucherCostCoins,
            VoucherReferenceLkr = SampleVoucherPolicy.ReferenceValueLkr,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveUntilUtc = request.EffectiveUntilUtc,
            CreatedByUserId = actor,
            CreatedAtUtc = nowUtc
        };
        _db.CoinRuleVersions.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return MapRule(row);
    }

    public async Task<IReadOnlyList<CoinRuleVersionResponse>>
        GetRuleVersionsAsync(CancellationToken cancellationToken = default) =>
        await _db.CoinRuleVersions.AsNoTracking()
            .OrderByDescending(x => x.EffectiveFromUtc)
            .Select(x => new CoinRuleVersionResponse(
                x.Id, x.VersionCode, x.VoucherCostCoins,
                x.VoucherReferenceLkr, x.EffectiveFromUtc,
                x.EffectiveUntilUtc, x.CreatedByUserId, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<CoinOperationsReport> GetOperationsReportAsync(
        CancellationToken cancellationToken = default)
    {
        long minted = await _db.CoinLedgerEntries.Where(x => x.DeltaCoins > 0)
            .SumAsync(x => (long?)x.DeltaCoins, cancellationToken) ?? 0;
        long debited = -(await _db.CoinLedgerEntries.Where(x => x.DeltaCoins < 0)
            .SumAsync(x => (long?)x.DeltaCoins, cancellationToken) ?? 0);
        long reserved = await _db.CoinReservations
            .Where(x => x.Status == CoinReservationState.Pending)
            .SumAsync(x => (long?)x.ReservedCoins, cancellationToken) ?? 0;
        long released = await _db.CoinReservations
            .Where(x => x.Status == CoinReservationState.Released)
            .SumAsync(x => (long?)x.ReservedCoins, cancellationToken) ?? 0;
        int pending = await _db.SampleVoucherRedemptionRequests.CountAsync(
            x => x.Status == SampleVoucherRequestStatusDraft.Pending,
            cancellationToken);
        int approvedUnissued = await _db.SampleVoucherRedemptionRequests.CountAsync(
            x => x.Status == SampleVoucherRequestStatusDraft.Approved &&
                 !_db.SampleVoucherIssuances.Any(i => i.RequestId == x.Id &&
                    (i.Status == SampleVoucherIssueStatusDraft.Issued ||
                     i.Status == SampleVoucherIssueStatusDraft.Used ||
                     i.Status == SampleVoucherIssueStatusDraft.Expired)),
            cancellationToken);
        int failed = await _db.SampleVoucherIssuances.CountAsync(
            x => x.Status == SampleVoucherIssueStatusDraft.Failed,
            cancellationToken);
        var reconciliation = await GetReconciliationReportAsync(
            DateTimeOffset.UtcNow, cancellationToken);
        return new CoinOperationsReport(
            minted, debited, reserved, released, pending,
            approvedUnissued, failed, reconciliation.Exceptions.Count);
    }

    public async Task<CoinReconciliationReport> GetReconciliationReportAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        RequireUtc(nowUtc);
        var exceptions = new List<CoinReconciliationException>();
        var wallets = await _db.CoinWallets.AsNoTracking()
            .Select(x => new { x.Id, x.ReservedCoins }).ToListAsync(cancellationToken);
        var pending = await _db.CoinReservations.AsNoTracking()
            .Where(x => x.Status == CoinReservationState.Pending)
            .GroupBy(x => x.WalletId)
            .Select(x => new { WalletId = x.Key, Amount = x.Sum(y => y.ReservedCoins) })
            .ToDictionaryAsync(x => x.WalletId, x => x.Amount, cancellationToken);
        foreach (var wallet in wallets)
        {
            pending.TryGetValue(wallet.Id, out long amount);
            if (wallet.ReservedCoins != amount)
                exceptions.Add(new("WALLET_RESERVATION_MISMATCH",
                    wallet.Id.ToString("D"),
                    $"Wallet={wallet.ReservedCoins}; reservations={amount}."));
        }

        var approved = await _db.SampleVoucherRedemptionRequests.AsNoTracking()
            .Where(x => x.Status == SampleVoucherRequestStatusDraft.Approved)
            .Select(x => new { x.Id, x.WalletId, x.CostCoins }).ToListAsync(cancellationToken);
        foreach (var request in approved)
        {
            bool debit = await _db.CoinLedgerEntries.AsNoTracking().AnyAsync(
                x => x.WalletId == request.WalletId &&
                     x.SourceRecordId == $"voucher-debit:{request.Id:D}" &&
                     x.DeltaCoins == -request.CostCoins, cancellationToken);
            if (!debit)
                exceptions.Add(new("APPROVED_WITHOUT_DEBIT", request.Id.ToString("D"),
                    "Approved voucher request has no authoritative debit."));
        }
        return new CoinReconciliationReport(nowUtc, exceptions);
    }

    private static CoinLedgerCorrectionResponse MapCorrection(
        CoinLedgerEntryRow correction, CoinLedgerEntryRow original,
        CoinWalletRow wallet) => new(
            correction.Id, original.Id, wallet.UserId,
            correction.DeltaCoins, wallet.PostedCoins,
            wallet.LifetimeEarnedCoins, correction.RecordedAtUtc);

    private static CoinRuleVersionResponse MapRule(CoinRuleVersionRow x) => new(
        x.Id, x.VersionCode, x.VoucherCostCoins, x.VoucherReferenceLkr,
        x.EffectiveFromUtc, x.EffectiveUntilUtc,
        x.CreatedByUserId, x.CreatedAtUtc);

    private static string RequireActor(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new UnauthorizedAccessException("Trusted actor is required.")
            : value.Trim();

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Valid UTC timestamp is required.");
    }
}
