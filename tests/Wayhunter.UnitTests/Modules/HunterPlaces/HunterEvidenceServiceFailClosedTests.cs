using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterEvidenceServiceFailClosedTests
{
    private sealed class Disabled : IHunterMediaReceiptVerifier
    {
        public bool IsConfigured => false;
        public Task<VerifiedHunterMediaReceipt?> VerifyAsync(string opaqueReceipt, string ownerUserId,
            Guid submissionId, Guid captureSessionId, CancellationToken cancellationToken = default) => Task.FromResult<VerifiedHunterMediaReceipt?>(null);
    }
    private sealed class Store : IHunterEvidenceStore
    {
        public int Calls { get; private set; }
        public Task<HunterEvidenceBindResult> BindAsync(Guid submissionId, Guid sessionId, string ownerUserId,
            Guid hunterId, VerifiedHunterMediaReceipt receipt, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new HunterEvidenceBindResult(true, false, Guid.NewGuid())); }
        public Task<bool> HasCompleteVerifiedEvidenceAsync(Guid submissionId, string ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    [Fact]
    public async Task MissingProviderVerifierNeverPersistsClientReceipt()
    {
        var store = new Store();
        var service = new HunterEvidenceService(new Disabled(), store, TimeProvider.System);
        await Assert.ThrowsAsync<HunterMediaUnavailableException>(() => service.BindAsync(
            Guid.NewGuid().ToString("D"), Guid.NewGuid(), new HunterEvidenceBindRequestDto
            { CaptureSessionId = Guid.NewGuid(), EvidenceType = Wayhunter.Domain.Modules.HunterPlaces.Enums.HunterEvidenceType.Guided360Video, ProviderReceipt = "client supplied" }));
        Assert.Equal(0, store.Calls);
    }
}
