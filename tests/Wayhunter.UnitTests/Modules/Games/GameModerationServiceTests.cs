
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameModerationServiceTests
{
    private static readonly DateTimeOffset ModeratedAt =
        new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    // =====================================
    // VALID MODERATION COMMAND
    // =====================================

    private static GameModerationCommand ValidCommand(
        GameModerationAction action =
            GameModerationAction.Reject)
    {
        return new GameModerationCommand(
            DecisionId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            AdminUserId: "admin-001",
            Action: action,
            Reason: "Unsafe game content",
            OccurredAtUtc: ModeratedAt,
            CorrelationId: Guid.NewGuid()
        );
    }

    // =====================================
    // FAKE MODERATION STORE
    // =====================================

    private sealed class FakeModerationStore
        : IGameModerationStore
    {
        public int CallCount { get; private set; }

        public GameModerationCommand? ReceivedCommand
        { get; private set; }

        public CancellationToken ReceivedToken
        { get; private set; }

        public Exception? ErrorToThrow { get; set; }

        public Task<GameModerationAuditRecord> ApplyAsync(
            GameModerationCommand command,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedCommand = command;
            ReceivedToken = cancellationToken;

            if (ErrorToThrow is not null)
            {
                throw ErrorToThrow;
            }

            // Simulated result only.
            // Does not update a real Game or database.
            var resultingStatus = command.Action switch
            {
                GameModerationAction.Reject =>
                    GameStatus.Rejected,

                GameModerationAction.Flag =>
                    GameStatus.PendingApproval,

                GameModerationAction.Disable =>
                    GameStatus.Disabled,

                _ => throw new InvalidOperationException(
                    "Unsupported action.")
            };

            var audit = new GameModerationAuditRecord(
                DecisionId: command.DecisionId,
                GameId: command.GameId,
                AdminUserId: command.AdminUserId,
                Action: command.Action,
                Reason: command.Reason,
                OccurredAtUtc: command.OccurredAtUtc,
                ResultingStatus: resultingStatus,
                CorrelationId: command.CorrelationId
            );

            return Task.FromResult(audit);
        }
    }

    // =====================================
    // TEST 01 - VALID REJECTION
    // =====================================

    [Fact]
    public async Task ValidReject_ShouldForwardToStore()
    {
        var store = new FakeModerationStore();
        var service = new GameModerationService(store);
        var command = ValidCommand();

        var result = await service.ModerateAsync(command);

        Assert.Equal(1, store.CallCount);

        Assert.Equal(command, store.ReceivedCommand);

        Assert.Equal(
            GameModerationAction.Reject,
            result.Action);

        Assert.Equal(
            GameStatus.Rejected,
            result.ResultingStatus);

        Assert.Equal(
            command.DecisionId,
            result.DecisionId);

        Assert.Equal(
            command.GameId,
            result.GameId);

        Assert.Equal(
            command.Reason,
            result.Reason);
    }

    // =====================================
    // TEST 02 - NORMALIZE INPUT
    // =====================================

    [Fact]
    public async Task ValidCommand_ShouldTrimTextFields()
    {
        var store = new FakeModerationStore();
        var service = new GameModerationService(store);

        var command = ValidCommand() with
        {
            AdminUserId = " admin-001 ",
            Reason = " Unsafe game content "
        };

        var result = await service.ModerateAsync(command);

        Assert.NotNull(store.ReceivedCommand);

        Assert.Equal(
            "admin-001",
            store.ReceivedCommand.AdminUserId);

        Assert.Equal(
            "Unsafe game content",
            store.ReceivedCommand.Reason);

        Assert.Equal(
            "Unsafe game content",
            result.Reason);
    }

    // =====================================
    // TEST 03 - NULL STORE
    // =====================================

    [Fact]
    public void NullStore_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameModerationService(null!));
    }

    // =====================================
    // TEST 04 - NULL COMMAND
    // =====================================

    [Fact]
    public async Task NullCommand_ShouldBeRejected()
    {
        var store = new FakeModerationStore();
        var service = new GameModerationService(store);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ModerateAsync(null!));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 05 - INVALID COMMAND FIELDS
    // 9 TEST CASES
    // =====================================

    [Theory]
    [InlineData("decision-id")]
    [InlineData("game-id")]
    [InlineData("admin-id")]
    [InlineData("action")]
    [InlineData("reason")]
    [InlineData("reason-too-long")]
    [InlineData("timestamp-default")]
    [InlineData("timestamp-non-utc")]
    [InlineData("correlation-id")]
    public async Task InvalidCommand_ShouldNotCallStore(
        string invalidField)
    {
        var store = new FakeModerationStore();
        var service = new GameModerationService(store);

        var valid = ValidCommand();

        var invalid = invalidField switch
        {
            "decision-id" => valid with
            {
                DecisionId = Guid.Empty
            },

            "game-id" => valid with
            {
                GameId = Guid.Empty
            },

            "admin-id" => valid with
            {
                AdminUserId = " "
            },

            "action" => valid with
            {
                Action = (GameModerationAction)999
            },

            "reason" => valid with
            {
                Reason = ""
            },

            "reason-too-long" => valid with
            {
                Reason = new string('X', 1001)
            },

            "timestamp-default" => valid with
            {
                OccurredAtUtc = default
            },

            "timestamp-non-utc" => valid with
            {
                OccurredAtUtc = new DateTimeOffset(
                    2026, 11, 14, 15, 30, 0,
                    TimeSpan.FromMinutes(330))
            },

            "correlation-id" => valid with
            {
                CorrelationId = Guid.Empty
            },

            _ => throw new ArgumentOutOfRangeException(
                nameof(invalidField))
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ModerateAsync(invalid));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 06 - CANCELLATION
    // =====================================

    [Fact]
    public async Task CancelledRequest_ShouldNotCallStore()
    {
        var store = new FakeModerationStore();
        var service = new GameModerationService(store);

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ModerateAsync(
                ValidCommand(),
                cts.Token));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 07 - FORWARD CANCELLATION TOKEN
    // =====================================

    [Fact]
    public async Task CancellationToken_ShouldReachStore()
    {
        var store = new FakeModerationStore();
        var service = new GameModerationService(store);

        using var cts = new CancellationTokenSource();

        await service.ModerateAsync(
            ValidCommand(),
            cts.Token);

        Assert.Equal(
            cts.Token,
            store.ReceivedToken);

        Assert.Equal(1, store.CallCount);
    }

    // =====================================
    // TEST 08 - STORE ERROR
    // =====================================

    [Fact]
    public async Task StoreFailure_ShouldPropagate()
    {
        var error = new InvalidOperationException(
            "Game is not eligible for moderation.");

        var store = new FakeModerationStore
        {
            ErrorToThrow = error
        };

        var service = new GameModerationService(store);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ModerateAsync(
                    ValidCommand()));

        Assert.Same(error, exception);
        Assert.Equal(1, store.CallCount);
    }

    // =====================================
    // TEST 09 - FLAG AND DISABLE
    // 2 TEST CASES
    // =====================================

    [Theory]
    [InlineData(
        GameModerationAction.Flag,
        GameStatus.PendingApproval)]
    [InlineData(
        GameModerationAction.Disable,
        GameStatus.Disabled)]
    public async Task ValidAction_ShouldReachStore(
        GameModerationAction action,
        GameStatus expectedStatus)
    {
        var store = new FakeModerationStore();
        var service = new GameModerationService(store);

        var command = ValidCommand(action);

        var result = await service.ModerateAsync(command);

        Assert.Equal(1, store.CallCount);
        Assert.Equal(action, result.Action);

        Assert.Equal(
            expectedStatus,
            result.ResultingStatus);

        Assert.Equal(
            command.DecisionId,
            result.DecisionId);
    }
}
