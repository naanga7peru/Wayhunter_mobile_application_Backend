
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinLedgerIdempotencyPolicyTests
{
    [Fact]
    public void SameRewardSource_HasSameKey()
    {
        var first = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            "hunt-001");

        var retry = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            "hunt-001");

        Assert.Equal(first, retry);
    }

    [Fact]
    public void SourceWhitespace_IsNormalized()
    {
        var first = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            "hunt-001");

        var retry = new CoinRewardUniqueKey(
            " user-001 ",
            CoinRewardType.HunterApproved,
            " hunt-001 ");

        Assert.Equal(first, retry);
    }

    [Fact]
    public void DifferentRecipients_HaveDifferentKeys()
    {
        var first = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            "hunt-001");

        var second = new CoinRewardUniqueKey(
            "user-002",
            CoinRewardType.HunterApproved,
            "hunt-001");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentSources_HaveDifferentKeys()
    {
        var first = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            "hunt-001");

        var second = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            "hunt-002");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentRewardTypes_HaveDifferentKeys()
    {
        var hunter = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            "source-001");

        var game = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.GameCreatorApproved,
            "source-001");

        Assert.NotEqual(hunter, game);
    }

    [Fact]
    public void MissingRecipient_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoinRewardUniqueKey(
                "",
                CoinRewardType.HunterApproved,
                "hunt-001"));
    }

    [Fact]
    public void MissingSource_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoinRewardUniqueKey(
                "user-001",
                CoinRewardType.HunterApproved,
                ""));
    }

    [Fact]
    public void InvalidRewardType_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoinRewardUniqueKey(
                "user-001",
                (CoinRewardType)999,
                "source-001"));
    }

    [Fact]
    public void RuleVersion_IsNormalized()
    {
        Assert.Equal(
            "v1",
            CoinLedgerVersionPolicy
                .RequireRuleVersion(" v1 "));
    }

    [Fact]
    public void EmptyRuleVersion_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CoinLedgerVersionPolicy
                .RequireRuleVersion(""));
    }

    [Fact]
    public void MatchingRuleVersions_AreAccepted()
    {
        Assert.True(
            CoinLedgerVersionPolicy
                .MatchesRuleVersion("v1", "v1"));
    }

    [Fact]
    public void DifferentRuleVersions_AreDetected()
    {
        Assert.False(
            CoinLedgerVersionPolicy
                .MatchesRuleVersion("v1", "v2"));
    }

    [Fact]
    public void MatchingWalletVersions_AreAccepted()
    {
        Assert.True(
            CoinLedgerVersionPolicy
                .MatchesWalletVersion(5, 5));
    }

    [Fact]
    public void StaleWalletVersion_IsDetected()
    {
        Assert.False(
            CoinLedgerVersionPolicy
                .MatchesWalletVersion(6, 5));
    }

    [Fact]
    public void NegativeWalletVersion_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoinLedgerVersionPolicy
                .MatchesWalletVersion(-1, 0));
    }
}
