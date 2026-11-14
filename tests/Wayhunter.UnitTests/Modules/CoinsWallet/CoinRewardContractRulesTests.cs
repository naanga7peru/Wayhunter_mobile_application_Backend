
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinRewardContractRulesTests
{
    private static CoinRewardGrantRequest ValidWinner()
    {
        return new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: CoinRewardType.GameWinnerApproved,
            SourceRecordId: Guid.NewGuid().ToString("D"),
            RecipientUserId: "player-001",
            CoinsAmount: 100,
            EligibilityConfirmedAtUtc: new DateTimeOffset(
                2026, 11, 13, 10, 0, 0, TimeSpan.Zero),
            CorrelationId: Guid.NewGuid(),
            RuleVersion: "2026-11-11",
            ApprovalRecordId: Guid.NewGuid().ToString("D"),
            GameAttemptId: Guid.NewGuid().ToString("D")
        );
    }

    [Theory]
    [InlineData(CoinRewardType.WelcomeVerified, "welcome:user-1")]
    [InlineData(CoinRewardType.HunterApproved, "hunter:user-1")]
    [InlineData(CoinRewardType.GameCreatorApproved, "game-creator:user-1")]
    [InlineData(CoinRewardType.GameWinnerApproved, "game-winner:user-1")]
    public void SourceKey_HasCorrectPrefix(
        CoinRewardType rewardType,
        string expected)
    {
        var key = CoinRewardContractRules.BuildIdempotencyKey(
            rewardType, "user-1");

        Assert.Equal(expected, key);
    }

    [Fact]
    public void SameSource_HasSameKeyOnRetry()
    {
        var first = CoinRewardContractRules.BuildIdempotencyKey(
            CoinRewardType.GameWinnerApproved, "game-001");

        var retry = CoinRewardContractRules.BuildIdempotencyKey(
            CoinRewardType.GameWinnerApproved, "game-001");

        Assert.Equal(first, retry);
    }

    [Fact]
    public void EmptySourceId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CoinRewardContractRules.BuildIdempotencyKey(
                CoinRewardType.HunterApproved, ""));
    }

    [Fact]
    public void ValidWinnerEvent_IsAccepted()
    {
        var request = ValidWinner();

        CoinRewardContractRules.ValidateGrant(request);
    }

    [Fact]
    public void IncorrectAmount_IsRejected()
    {
        var request = ValidWinner() with { CoinsAmount = 200 };

        Assert.Throws<ArgumentException>(() =>
            CoinRewardContractRules.ValidateGrant(request));
    }

    [Fact]
    public void WinnerWithoutAttempt_IsRejected()
    {
        var request = ValidWinner() with { GameAttemptId = null };

        Assert.Throws<ArgumentException>(() =>
            CoinRewardContractRules.ValidateGrant(request));
    }

    [Fact]
    public void WinnerWithoutAdminReview_IsRejected()
    {
        var request = ValidWinner() with { ApprovalRecordId = null };

        Assert.Throws<ArgumentException>(() =>
            CoinRewardContractRules.ValidateGrant(request));
    }

    [Fact]
    public void NonUtcTimestamp_IsRejected()
    {
        var request = ValidWinner() with
        {
            EligibilityConfirmedAtUtc = DateTimeOffset.Now
                .ToOffset(TimeSpan.FromHours(5.5))
        };

        Assert.Throws<ArgumentException>(() =>
            CoinRewardContractRules.ValidateGrant(request));
    }

    [Fact]
    public void CorrectionWithoutReason_IsRejected()
    {
        var request = new CoinRewardCorrectionRequest(
            Guid.NewGuid(),
            "admin-001",
            "",
            Guid.NewGuid());

        Assert.Throws<ArgumentException>(() =>
            CoinRewardContractRules.ValidateCorrectionProposal(request));
    }
}
