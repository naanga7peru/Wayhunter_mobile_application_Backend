
using System;

namespace Wayhunter.Domain.Modules.CoinsWallet.Entities;

/// <summary>
/// Draft compensating correction for an incorrectly
/// granted Coins reward.
///
/// Existing ledger entries remain unchanged.
/// This model does NOT write to SQL Server.
///
/// Only positive reward credits can be corrected.
/// Voucher debit refunds are not approved.
/// </summary>
public sealed class CoinRewardCorrectionEntryDraft
{
    public Guid OriginalLedgerEntryId { get; }
    public Guid CorrelationId { get; }
    public string AdminUserId { get; }
    public string Reason { get; }

    public CoinLedgerEntrySchemaDraft ReversalEntry { get; }

    private CoinRewardCorrectionEntryDraft(
        Guid originalLedgerEntryId,
        Guid correlationId,
        string adminUserId,
        string reason,
        CoinLedgerEntrySchemaDraft reversalEntry)
    {
        OriginalLedgerEntryId = originalLedgerEntryId;
        CorrelationId = correlationId;
        AdminUserId = adminUserId;
        Reason = reason;
        ReversalEntry = reversalEntry;
    }

    public static CoinRewardCorrectionEntryDraft Create(
        CoinLedgerEntrySchemaDraft original,
        Guid correctionEntryId,
        string adminUserId,
        string reason,
        Guid correlationId,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(original);

        // No automatic refund or voucher debit reversal.
        if (original.DeltaCoins <= 0 ||
            !original.SourceRecordId.StartsWith(
                "reward:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only positive reward credits can be corrected.");
        }

        if (correctionEntryId == Guid.Empty ||
            correctionEntryId == original.Id)
        {
            throw new ArgumentException(
                "A new correction entry ID is required.",
                nameof(correctionEntryId));
        }

        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new ArgumentException(
                "Admin user ID is required.",
                nameof(adminUserId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Correction reason is required.",
                nameof(reason));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));
        }

        if (recordedAtUtc == default ||
            recordedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC timestamp is required.",
                nameof(recordedAtUtc));
        }

        // A positive reward credit gets an equal
        // negative compensating ledger entry.
        long correctionCoins =
            checked(-original.DeltaCoins);

        // Reference the original immutable ledger row.
        var reversal = new CoinLedgerEntrySchemaDraft(
            correctionEntryId,
            original.WalletId,
            correctionCoins,
            $"reward-correction:{original.Id:N}",
            original.RuleVersion,
            recordedAtUtc);

        return new CoinRewardCorrectionEntryDraft(
            original.Id,
            correlationId,
            adminUserId.Trim(),
            reason.Trim(),
            reversal);
    }
}
