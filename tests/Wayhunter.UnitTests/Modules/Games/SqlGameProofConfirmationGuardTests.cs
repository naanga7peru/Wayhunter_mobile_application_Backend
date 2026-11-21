
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class SqlGameProofConfirmationGuardTests
{
    private const string Connection =
        @"Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_M5_IntegrationTest;Integrated Security=True";

    private static SqlGameProofSubmissionOperationStore Store() =>
        new(Connection);

    private static GameProofSubmissionOperationBinding Binding() =>
        new(
            Guid.NewGuid(),
            "player-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "capture-1",
            "provider-1",
            "media-1");

    private static GameProofCaptureOperationOutcome Consumed(
        GameProofSubmissionOperationBinding binding,
        string? receipt = "receipt-1") =>
        new(
            binding.SubmissionOperationId,
            GameProofCaptureOperationOutcomeStatus.Consumed,
            receipt);

    [Theory]
    [InlineData(GameProofCaptureOperationOutcomeStatus.Unknown)]
    [InlineData(GameProofCaptureOperationOutcomeStatus.Rejected)]
    public async Task NonConsumedOutcome_ShouldRejectBeforeSql(
        GameProofCaptureOperationOutcomeStatus status)
    {
        var binding = Binding();

        var outcome = new GameProofCaptureOperationOutcome(
            binding.SubmissionOperationId,
            status,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding, "claim-1", outcome));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingReceipt_ShouldRejectBeforeSql(
        string? receipt)
    {
        var binding = Binding();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding, "claim-1", Consumed(binding, receipt)));
    }

    [Fact]
    public async Task DifferentOperation_ShouldRejectBeforeSql()
    {
        var binding = Binding();

        var outcome = new GameProofCaptureOperationOutcome(
            Guid.NewGuid(),
            GameProofCaptureOperationOutcomeStatus.Consumed,
            "receipt-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding, "claim-1", outcome));
    }

    [Fact]
    public async Task OversizedReceipt_ShouldRejectBeforeSql()
    {
        var binding = Binding();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding,
                "claim-1",
                Consumed(binding, new string('r', 257))));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyClaim_ShouldRejectBeforeSql(
        string token)
    {
        var binding = Binding();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding, token, Consumed(binding)));
    }

    [Fact]
    public async Task OversizedClaim_ShouldRejectBeforeSql()
    {
        var binding = Binding();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding,
                new string('x', 129),
                Consumed(binding)));
    }

    [Fact]
    public async Task InvalidBinding_ShouldRejectBeforeSql()
    {
        var binding = Binding() with
        {
            SubmissionOperationId = Guid.Empty
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding, "claim-1", Consumed(binding)));
    }

    [Fact]
    public async Task CancelledRequest_ShouldRejectBeforeSql()
    {
        var binding = Binding();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Store().TryRecordConfirmedConsumptionAsync(
                binding,
                "claim-1",
                Consumed(binding),
                cts.Token));
    }
}
