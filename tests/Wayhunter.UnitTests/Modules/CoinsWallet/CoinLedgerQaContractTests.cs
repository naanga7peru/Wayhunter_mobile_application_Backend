
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinLedgerQaContractTests
{
    [Fact]
    public void SameRewardSource_ProducesSameKey()
    {
        string first = CoinRewardContractRules
            .BuildIdempotencyKey(
                CoinRewardType.HunterApproved,
                "hunt-42");

        string retry = CoinRewardContractRules
            .BuildIdempotencyKey(
                CoinRewardType.HunterApproved,
                " hunt-42 ");

        Assert.Equal(first, retry);
    }

    [Fact]
    public void DifferentSources_ProduceDifferentKeys()
    {
        string first = CoinRewardContractRules
            .BuildIdempotencyKey(
                CoinRewardType.HunterApproved,
                "hunt-42");

        string second = CoinRewardContractRules
            .BuildIdempotencyKey(
                CoinRewardType.HunterApproved,
                "hunt-43");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentRewardTypes_ProduceDifferentKeys()
    {
        string hunter = CoinRewardContractRules
            .BuildIdempotencyKey(
                CoinRewardType.HunterApproved,
                "source-1");

        string game = CoinRewardContractRules
            .BuildIdempotencyKey(
                CoinRewardType.GameCreatorApproved,
                "source-1");

        Assert.NotEqual(hunter, game);
    }

    [Fact]
    public void SecondReservation_InsufficientOnMinimumWallet()
    {
        var initial = new CoinWalletBalance(
            10100, 0, 10100);

        var afterHold = new CoinWalletBalance(
            10100, 10000, 10100);

        Assert.True(
            CoinLevelRedemptionRules
                .CanRequestSampleVoucher(initial));

        Assert.False(
            CoinLevelRedemptionRules
                .CanRequestSampleVoucher(afterHold));

        Assert.Equal(100, afterHold.SpendableCoins);
    }

    [Fact]
    public void OverReservedWallet_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoinWalletBalance(
                10100, 20000, 10100));
    }

    [Fact]
    public void RejectionProjection_ReleasesHold()
    {
        var pending = new CoinWalletBalance(
            10100, 10000, 10100);

        var rejected = new CoinWalletBalance(
            10100, 0, 10100);

        Assert.Equal(100, pending.SpendableCoins);
        Assert.Equal(10100, rejected.SpendableCoins);
    }


    [Fact]
    public void ApprovalPreservesHold_FulfillmentConsumesIt()
    {
        var pending = new CoinWalletBalance(
            10100, 10000, 10100);

        var approved = new CoinWalletBalance(
            10100, 10000, 10100);

        var issued = new CoinWalletBalance(
            100, 0, 10100);

        Assert.Equal(pending, approved);

        Assert.Equal(10100, approved.PostedCoins);
        Assert.Equal(10000, approved.ReservedCoins);

        Assert.Equal(100, issued.PostedCoins);
        Assert.Equal(0, issued.ReservedCoins);

        Assert.Equal(
            approved.SpendableCoins,
            issued.SpendableCoins);
    }



    [Fact]
    public void OnlySuccessfulFulfillmentStage_CanTriggerDebit()
    {
        Assert.False(
            SampleVoucherPolicy.IsFinalDebitTrigger(
                SampleVoucherAction.AdminApprove));

        Assert.False(
            SampleVoucherPolicy.IsFinalDebitTrigger(
                SampleVoucherAction.Request));

        Assert.False(
            SampleVoucherPolicy.IsFinalDebitTrigger(
                SampleVoucherAction.RejectOrCancel));

        Assert.True(
            SampleVoucherPolicy.IsFinalDebitTrigger(
                SampleVoucherAction.Issue));

        Assert.True(
            SampleVoucherPolicy.IsFinalDebitTrigger(
                SampleVoucherAction.RetryIssue));
    }

    [Fact]
    public void Issuance_RequiresApprovalAndCommittedDebit()
    {
        Assert.False(
            SampleVoucherPolicy.CanIssue(false, false));

        Assert.False(
            SampleVoucherPolicy.CanIssue(true, false));

        Assert.False(
            SampleVoucherPolicy.CanIssue(false, true));

        Assert.True(
            SampleVoucherPolicy.CanIssue(true, true));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(10099, false)]
    [InlineData(10100, true)]
    public void RedemptionThreshold_IsEnforced(
        long spendable,
        bool expected)
    {
        var wallet = new CoinWalletBalance(
            spendable, 0, spendable);

        Assert.Equal(expected,
            CoinLevelRedemptionRules
                .CanRequestSampleVoucher(wallet));
    }
}
