
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameApprovalServiceTests
{
    private static readonly DateTimeOffset ApprovedAt =
        new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    // =====================================
    // VALID COMMAND FACTORY
    // =====================================

    private static GameApprovalCommand ValidCommand()
    {
        return new GameApprovalCommand(
            GameId: Guid.NewGuid(),
            AdminUserId: "admin-001",
            ApprovalRecordId: "approval-001",
            ApprovedAtUtc: ApprovedAt,
            RewardEventId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            RewardRuleVersion: "game-creator-v1"
        );
    }

    // =====================================
    // FAKE STORE FOR UNIT TESTS
    // =====================================

    private sealed class FakeGameApprovalStore
        : IGameApprovalStore
    {
        public int CallCount { get; private set; }

        public GameApprovalCommand? ReceivedCommand
        { get; private set; }

        public CancellationToken ReceivedToken
        { get; private set; }

        public Exception? ErrorToThrow { get; set; }

        public Task<CoinRewardGrantRequest>
            ApproveAndEnqueueCreatorRewardAsync(
                GameApprovalCommand command,
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
            // No Game or database is changed.
            var reward = new CoinRewardGrantRequest(
                EventId: command.RewardEventId,
                RewardType:
                    CoinRewardType.GameCreatorApproved,
                SourceRecordId:
                    command.GameId.ToString("D"),
                RecipientUserId: "creator-001",
                CoinsAmount: 100,
                EligibilityConfirmedAtUtc:
                    command.ApprovedAtUtc,
                CorrelationId: command.CorrelationId,
                RuleVersion: command.RewardRuleVersion,
                ApprovalRecordId:
                    command.ApprovalRecordId
            );

            return Task.FromResult(reward);
        }
    }

    // =====================================
    // TEST 01 - VALID APPROVAL
    // =====================================

    [Fact]
    public async Task ValidCommand_ShouldForwardToStore()
    {
        var store = new FakeGameApprovalStore();
        var service = new GameApprovalService(store);
        var command = ValidCommand();

        var result = await service.ApproveAsync(command);

        Assert.Equal(1, store.CallCount);
        Assert.Equal(command, store.ReceivedCommand);

        Assert.Equal(
            CoinRewardType.GameCreatorApproved,
            result.RewardType);

        Assert.Equal(100, result.CoinsAmount);
        Assert.Equal(command.RewardEventId, result.EventId);
        Assert.Equal(
            command.ApprovalRecordId,
            result.ApprovalRecordId);
    }

    // =====================================
    // TEST 02 - TRIM INPUT VALUES
    // =====================================

    [Fact]
    public async Task ValidCommand_ShouldTrimTextFields()
    {
        var store = new FakeGameApprovalStore();
        var service = new GameApprovalService(store);

        var command = ValidCommand() with
        {
            AdminUserId = " admin-001 ",
            ApprovalRecordId = " approval-001 ",
            RewardRuleVersion = " game-creator-v1 "
        };

        await service.ApproveAsync(command);

        Assert.NotNull(store.ReceivedCommand);

        Assert.Equal(
            "admin-001",
            store.ReceivedCommand.AdminUserId);

        Assert.Equal(
            "approval-001",
            store.ReceivedCommand.ApprovalRecordId);

        Assert.Equal(
            "game-creator-v1",
            store.ReceivedCommand.RewardRuleVersion);
    }

    // =====================================
    // TEST 03 - NULL STORE
    // =====================================

    [Fact]
    public void NullStore_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameApprovalService(null!));
    }

    // =====================================
    // TEST 04 - NULL COMMAND
    // =====================================

    [Fact]
    public async Task NullCommand_ShouldBeRejected()
    {
        var store = new FakeGameApprovalStore();
        var service = new GameApprovalService(store);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ApproveAsync(null!));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 05 - INVALID COMMAND FIELDS
    // =====================================

    [Theory]
    [InlineData("game-id")]
    [InlineData("admin-id")]
    [InlineData("approval-id")]
    [InlineData("timestamp-default")]
    [InlineData("timestamp-non-utc")]
    [InlineData("event-id")]
    [InlineData("correlation-id")]
    [InlineData("rule-version")]
    public async Task InvalidCommand_ShouldNotCallStore(
        string invalidField)
    {
        var store = new FakeGameApprovalStore();
        var service = new GameApprovalService(store);

        var valid = ValidCommand();

        var invalid = invalidField switch
        {
            "game-id" => valid with
            {
                GameId = Guid.Empty
            },

            "admin-id" => valid with
            {
                AdminUserId = " "
            },

            "approval-id" => valid with
            {
                ApprovalRecordId = ""
            },

            "timestamp-default" => valid with
            {
                ApprovedAtUtc = default
            },

            "timestamp-non-utc" => valid with
            {
                ApprovedAtUtc = new DateTimeOffset(
                    2026, 11, 14, 15, 30, 0,
                    TimeSpan.FromHours(5.5))
            },

            "event-id" => valid with
            {
                RewardEventId = Guid.Empty
            },

            "correlation-id" => valid with
            {
                CorrelationId = Guid.Empty
            },

            "rule-version" => valid with
            {
                RewardRuleVersion = " "
            },

            _ => throw new ArgumentOutOfRangeException(
                nameof(invalidField))
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ApproveAsync(invalid));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 06 - CANCELLATION
    // =====================================

    [Fact]
    public async Task CancelledRequest_ShouldNotCallStore()
    {
        var store = new FakeGameApprovalStore();
        var service = new GameApprovalService(store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ApproveAsync(
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
        var store = new FakeGameApprovalStore();
        var service = new GameApprovalService(store);

        using var cts = new CancellationTokenSource();

        await service.ApproveAsync(
            ValidCommand(),
            cts.Token);

        Assert.Equal(
            cts.Token,
            store.ReceivedToken);
    }

    // =====================================
    // TEST 08 - STORE FAILURE
    // =====================================

    [Fact]
    public async Task StoreFailure_ShouldPropagate()
    {
        var store = new FakeGameApprovalStore
        {
            ErrorToThrow = new InvalidOperationException(
                "Game is not pending approval.")
        };

        var service = new GameApprovalService(store);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ApproveAsync(
                    ValidCommand()));

        Assert.Equal(
            "Game is not pending approval.",
            exception.Message);

        Assert.Equal(1, store.CallCount);
    }
}
