
using System;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameCreatorApprovalRewardFactoryTests
{
    private const string CreatorId = "creator-001";

    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAt =
        SubmittedAt.AddHours(2);

    // =====================================
    // CREATE VALID GAME DRAFT
    // =====================================

    private static Game NewDraft()
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Hunt",
            "Find the hidden locations",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the first location",
            null);

        game.ReplaceDraftStages(
            CreatorId,
            new[] { step },
            new[] { hint });

        return game;
    }

    private static Game NewSubmittedGame()
    {
        var game = NewDraft();

        game.SubmitForAdminReview(
            CreatorId,
            SubmittedAt);

        return game;
    }

    private static Game NewApprovedGame()
    {
        var game = NewSubmittedGame();

        game.ApproveCreation(ApprovedAt);

        return game;
    }

    private static CoinRewardGrantRequest CreateReward(
        Game game,
        string approvalRecordId = "approval-001",
        Guid? eventId = null,
        Guid? correlationId = null,
        string ruleVersion = "game-creator-v1")
    {
        return GameCreatorApprovalRewardFactory.Create(
            game,
            approvalRecordId,
            eventId ?? Guid.NewGuid(),
            correlationId ?? Guid.NewGuid(),
            ruleVersion);
    }

    // =====================================
    // TEST 01 - VALID CREATOR REWARD
    // =====================================

    [Fact]
    public void ApprovedGame_ShouldCreate100CoinReward()
    {
        var game = NewApprovedGame();

        var reward = CreateReward(game);

        Assert.Equal(
            CoinRewardType.GameCreatorApproved,
            reward.RewardType);

        Assert.Equal(100, reward.CoinsAmount);

        Assert.Equal(
            CreatorId,
            reward.RecipientUserId);

        Assert.Equal(
            game.Id.ToString("D"),
            reward.SourceRecordId);

        Assert.Equal(
            ApprovedAt,
            reward.EligibilityConfirmedAtUtc);

        Assert.Equal(
            "approval-001",
            reward.ApprovalRecordId);

        Assert.Equal(
            "game-creator-v1",
            reward.RuleVersion);

        Assert.NotEqual(Guid.Empty, reward.EventId);
        Assert.NotEqual(Guid.Empty, reward.CorrelationId);

        Assert.Null(reward.GameAttemptId);

        // Ensure the output follows M6 rules.
        CoinRewardContractRules.ValidateGrant(reward);
    }

    // =====================================
    // TEST 02 - STABLE IDEMPOTENCY KEY
    // =====================================

    [Fact]
    public void SameGame_ShouldProduceSameRewardSourceKey()
    {
        var game = NewApprovedGame();

        var first = CreateReward(game);
        var second = CreateReward(game);

        var firstKey =
            CoinRewardContractRules.BuildIdempotencyKey(
                first.RewardType,
                first.SourceRecordId);

        var secondKey =
            CoinRewardContractRules.BuildIdempotencyKey(
                second.RewardType,
                second.SourceRecordId);

        Assert.Equal(firstKey, secondKey);

        Assert.Equal(
            $"game-creator:{game.Id:D}",
            firstKey);
    }

    // =====================================
    // TEST 03 - NULL GAME
    // =====================================

    [Fact]
    public void NullGame_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => CreateReward(null!));
    }

    // =====================================
    // TEST 04 - DRAFT GAME
    // =====================================

    [Fact]
    public void DraftGame_ShouldNotReceiveReward()
    {
        var game = NewDraft();

        Assert.Throws<InvalidOperationException>(
            () => CreateReward(game));
    }

    // =====================================
    // TEST 05 - PENDING GAME
    // =====================================

    [Fact]
    public void PendingGame_ShouldNotReceiveReward()
    {
        var game = NewSubmittedGame();

        Assert.Throws<InvalidOperationException>(
            () => CreateReward(game));
    }

    // =====================================
    // TEST 06 - REJECTED GAME
    // =====================================

    [Fact]
    public void RejectedGame_ShouldNotReceiveReward()
    {
        var game = NewSubmittedGame();

        game.RejectCreation();

        Assert.Throws<InvalidOperationException>(
            () => CreateReward(game));
    }

    // =====================================
    // TEST 07 - EXPIRED GAME
    // =====================================

    [Fact]
    public void ExpiredGame_ShouldNotCreateNewRewardRequest()
    {
        var game = NewApprovedGame();

        var archived = game.ArchiveIfExpired(
            ApprovedAt.AddDays(30));

        Assert.True(archived);

        Assert.Throws<InvalidOperationException>(
            () => CreateReward(game));
    }

    // =====================================
    // TEST 08 - LEGACY APPROVAL
    // =====================================

    [Fact]
    public void LegacyPendingGame_ShouldNotReceiveReward()
    {
        var game = new Game(
            Guid.NewGuid(),
            CreatorId);

        game.ApproveCreation(ApprovedAt);

        // No Draft -> Pending submission record.
        Assert.Throws<InvalidOperationException>(
            () => CreateReward(game));
    }

    // =====================================
    // TEST 09 - APPROVAL BEFORE SUBMISSION
    // =====================================

    [Fact]
    public void EarlyApprovalTimestamp_ShouldBeRejected()
    {
        var game = NewSubmittedGame();

        game.ApproveCreation(
            SubmittedAt.AddMinutes(-1));

        Assert.Throws<InvalidOperationException>(
            () => CreateReward(game));
    }

    // =====================================
    // TEST 10 - MISSING APPROVAL RECORD
    // =====================================

    [Fact]
    public void MissingApprovalRecord_ShouldBeRejected()
    {
        var game = NewApprovedGame();

        Assert.Throws<ArgumentException>(
            () => CreateReward(
                game,
                approvalRecordId: " "));
    }

    // =====================================
    // TEST 11 - INVALID EVENT ID
    // =====================================

    [Fact]
    public void EmptyEventId_ShouldBeRejected()
    {
        var game = NewApprovedGame();

        Assert.Throws<ArgumentException>(
            () => CreateReward(
                game,
                eventId: Guid.Empty));
    }

    // =====================================
    // TEST 12 - INVALID CORRELATION ID
    // =====================================

    [Fact]
    public void EmptyCorrelationId_ShouldBeRejected()
    {
        var game = NewApprovedGame();

        Assert.Throws<ArgumentException>(
            () => CreateReward(
                game,
                correlationId: Guid.Empty));
    }

    // =====================================
    // TEST 13 - MISSING RULE VERSION
    // =====================================

    [Fact]
    public void MissingRuleVersion_ShouldBeRejected()
    {
        var game = NewApprovedGame();

        Assert.Throws<ArgumentException>(
            () => CreateReward(
                game,
                ruleVersion: " "));
    }
}
