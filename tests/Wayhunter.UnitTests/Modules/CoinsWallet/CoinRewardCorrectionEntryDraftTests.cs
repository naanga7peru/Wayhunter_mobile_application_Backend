
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinRewardCorrectionEntryDraftTests
{
    private static CoinLedgerEntrySchemaDraft CreateReward(
        long coins = 200,
        string source = "reward:HunterApproved:hunt-001")
    {
        return new CoinLedgerEntrySchemaDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            coins,
            source,
            "v1",
            DateTimeOffset.UtcNow);
    }

    private static CoinRewardCorrectionEntryDraft Correct(
        CoinLedgerEntrySchemaDraft original,
        Guid? correctionId = null,
        string admin = "admin-001",
        string reason = "Incorrect reward",
        Guid? correlationId = null)
    {
        return CoinRewardCorrectionEntryDraft.Create(
            original,
            correctionId ?? Guid.NewGuid(),
            admin,
            reason,
            correlationId ?? Guid.NewGuid(),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ValidCorrection_CreatesNegativeEntry()
    {
        var original = CreateReward();

        var correction = Correct(original);

        Assert.Equal(-200,
            correction.ReversalEntry.DeltaCoins);

        Assert.Equal(original.Id,
            correction.OriginalLedgerEntryId);

        Assert.Equal(original.WalletId,
            correction.ReversalEntry.WalletId);
    }

    [Fact]
    public void Correction_DoesNotModifyOriginalEntry()
    {
        var original = CreateReward();

        var originalId = original.Id;
        var originalCoins = original.DeltaCoins;
        var originalSource = original.SourceRecordId;

        var correction = Correct(original);

        Assert.Equal(originalId, original.Id);
        Assert.Equal(originalCoins, original.DeltaCoins);
        Assert.Equal(originalSource, original.SourceRecordId);

        Assert.NotEqual(
            original.Id,
            correction.ReversalEntry.Id);
    }

    [Fact]
    public void Correction_PreservesRuleVersion()
    {
        var original = CreateReward();

        var correction = Correct(original);

        Assert.Equal(
            original.RuleVersion,
            correction.ReversalEntry.RuleVersion);
    }

    [Fact]
    public void Correction_ReferencesOriginalLedgerId()
    {
        var original = CreateReward();

        var correction = Correct(original);

        Assert.Equal(
            $"reward-correction:{original.Id:N}",
            correction.ReversalEntry.SourceRecordId);
    }

    [Fact]
    public void Correction_StoresAuditDetails()
    {
        var original = CreateReward();

        var correction = Correct(
            original,
            admin: " admin-001 ",
            reason: " Wrong approval ");

        Assert.Equal("admin-001", correction.AdminUserId);
        Assert.Equal("Wrong approval", correction.Reason);

        Assert.NotEqual(Guid.Empty,
            correction.CorrelationId);
    }

    [Fact]
    public void VoucherDebit_CannotBeCorrectedAutomatically()
    {
        var original = CreateReward(
            -10000,
            "voucher-approved:request-001");

        Assert.Throws<InvalidOperationException>(
            () => Correct(original));
    }

    [Fact]
    public void NonRewardCredit_IsRejected()
    {
        var original = CreateReward(
            200,
            "manual-adjustment:001");

        Assert.Throws<InvalidOperationException>(
            () => Correct(original));
    }

    [Fact]
    public void EmptyCorrectionId_IsRejected()
    {
        var original = CreateReward();

        Assert.Throws<ArgumentException>(() =>
            Correct(
                original,
                correctionId: Guid.Empty));
    }

    [Fact]
    public void OriginalId_CannotBeReused()
    {
        var original = CreateReward();

        Assert.Throws<ArgumentException>(() =>
            Correct(
                original,
                correctionId: original.Id));
    }

    [Fact]
    public void MissingAdmin_IsRejected()
    {
        var original = CreateReward();

        Assert.Throws<ArgumentException>(() =>
            Correct(original, admin: ""));
    }

    [Fact]
    public void MissingReason_IsRejected()
    {
        var original = CreateReward();

        Assert.Throws<ArgumentException>(() =>
            Correct(original, reason: " "));
    }

    [Fact]
    public void EmptyCorrelationId_IsRejected()
    {
        var original = CreateReward();

        Assert.Throws<ArgumentException>(() =>
            Correct(
                original,
                correlationId: Guid.Empty));
    }

    [Fact]
    public void InvalidUtcTimestamp_IsRejected()
    {
        var original = CreateReward();

        Assert.Throws<ArgumentException>(() =>
            CoinRewardCorrectionEntryDraft.Create(
                original,
                Guid.NewGuid(),
                "admin-001",
                "Wrong reward",
                Guid.NewGuid(),
                DateTimeOffset.Now.ToOffset(
                    TimeSpan.FromHours(1))));
    }

    [Fact]
    public void NullOriginalEntry_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Correct(null!));
    }
}
