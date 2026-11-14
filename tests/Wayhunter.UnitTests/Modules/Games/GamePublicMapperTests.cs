
using System;
using System.Linq;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GamePublicMapperTests
{
    private static readonly DateTimeOffset ApprovedAt =
        new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static Game NewPendingGame()
    {
        return new Game(
            Guid.NewGuid(),
            "creator-1");
    }

    private static Game NewDraft()
    {
        return Game.CreateDraft(
            Guid.NewGuid(),
            "creator-1",
            "Jaffna Treasure Hunt",
            "Find the hidden locations",
            60);
    }

    // TEST 01 - NULL GAME

    [Fact]
    public void NullGame_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GamePublicMapper.ToAvailableSummary(
                null!,
                ApprovedAt));
    }

    // TEST 02 - SERVER UTC REQUIRED

    [Fact]
    public void NonUtcTime_ShouldBeRejected()
    {
        var game = NewPendingGame();

        var nonUtcTime = new DateTimeOffset(
            2026, 11, 14, 12, 0, 0,
            TimeSpan.FromMinutes(330));

        Assert.Throws<ArgumentException>(() =>
            GamePublicMapper.ToAvailableSummary(
                game,
                nonUtcTime));
    }

    // TEST 03 - DRAFT IS PRIVATE

    [Fact]
    public void DraftGame_ShouldNotBePublic()
    {
        var game = NewDraft();

        Assert.Throws<InvalidOperationException>(() =>
            GamePublicMapper.ToAvailableSummary(
                game,
                ApprovedAt));
    }

    // TEST 04 - PENDING GAME IS PRIVATE

    [Fact]
    public void PendingGame_ShouldNotBePublic()
    {
        var game = NewPendingGame();

        Assert.Throws<InvalidOperationException>(() =>
            GamePublicMapper.ToAvailableSummary(
                game,
                ApprovedAt));
    }

    // TEST 05 - REJECTED GAME IS PRIVATE

    [Fact]
    public void RejectedGame_ShouldNotBePublic()
    {
        var game = NewPendingGame();

        game.RejectCreation();

        Assert.Throws<InvalidOperationException>(() =>
            GamePublicMapper.ToAvailableSummary(
                game,
                ApprovedAt));
    }

    // TEST 06 - EXPIRED GAME IS HIDDEN

    [Fact]
    public void ExpiredGame_ShouldNotBePublic()
    {
        var game = NewPendingGame();

        game.ApproveCreation(ApprovedAt);

        var expiredAt = ApprovedAt.AddDays(30);

        Assert.Throws<InvalidOperationException>(() =>
            GamePublicMapper.ToAvailableSummary(
                game,
                expiredAt));
    }

    // TEST 07 - GAME WITH WINNER IS CLOSED

    [Fact]
    public void GameWithWinner_ShouldNotBePublic()
    {
        var game = NewPendingGame();

        game.ApproveCreation(ApprovedAt);

        game.ApproveVerifiedWinner(
            "winner-1",
            ApprovedAt.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            GamePublicMapper.ToAvailableSummary(
                game,
                ApprovedAt.AddMinutes(2)));
    }

    // TEST 08 - INCOMPLETE LEGACY GAME

    [Fact]
    public void IncompletePublishedGame_ShouldBeRejected()
    {
        var game = NewPendingGame();

        game.ApproveCreation(ApprovedAt);

        // Older pending-game constructor has no
        // title or duration. Public mapping must reject it.

        Assert.Throws<InvalidOperationException>(() =>
            GamePublicMapper.ToAvailableSummary(
                game,
                ApprovedAt.AddMinutes(1)));
    }

    // TEST 09 - PUBLIC DTO FIELD ALLOWLIST

    [Fact]
    public void PublicDto_ShouldOnlyExposeApprovedFields()
    {
        var actualFields =
            typeof(PublicGameSummaryDto)
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

        var expectedFields = new[]
        {
            "Description",
            "DurationMinutes",
            "ExpiresAtUtc",
            "GameId",
            "PublishedAtUtc",
            "Title"
        };

        Assert.Equal(expectedFields, actualFields);
    }
}
