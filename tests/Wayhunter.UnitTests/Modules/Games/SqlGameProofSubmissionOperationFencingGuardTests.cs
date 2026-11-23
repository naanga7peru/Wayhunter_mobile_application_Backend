using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;
namespace Wayhunter.UnitTests.Modules.Games;
public sealed class SqlGameProofSubmissionOperationFencingGuardTests
{
    private const string Allowed = @"Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Test_M5_00000000000000000000000000000000;Integrated Security=True";
    private static GameProofSubmissionOperationBinding Binding() => new(Guid.NewGuid(), "player-1", Guid.NewGuid(), Guid.NewGuid(), 1, "capture-1", "provider-1", "media-1");
    [Theory] [InlineData("")] [InlineData(" ")]
    public async Task EmptyTokenRejected(string token) { await Assert.ThrowsAsync<ArgumentException>(() => new SqlGameProofSubmissionOperationStore(Allowed).TryMarkProviderOutcomeUnknownAsync(Binding(), token)); }
    [Fact]
    public async Task LongTokenRejected() { await Assert.ThrowsAsync<ArgumentException>(() => new SqlGameProofSubmissionOperationStore(Allowed).TryMarkProviderOutcomeUnknownAsync(Binding(), new string('x',129))); }
    [Fact]
    public async Task InvalidBindingRejected() { await Assert.ThrowsAsync<ArgumentException>(() => new SqlGameProofSubmissionOperationStore(Allowed).TryMarkProviderOutcomeUnknownAsync(Binding() with { SubmissionOperationId = Guid.Empty }, "claim-1")); }
    [Fact]
    public async Task CancelledRequestRejected() { using var cts = new CancellationTokenSource(); cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqlGameProofSubmissionOperationStore(Allowed).TryMarkProviderOutcomeUnknownAsync(Binding(), "claim-1", cts.Token)); }
}
