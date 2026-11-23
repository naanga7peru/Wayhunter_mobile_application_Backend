using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;

namespace Wayhunter.Infrastructure.Integrations.HunterPlaces;

/// <summary>Fail-closed until a trusted private Hunter media provider is configured.</summary>
public sealed class DisabledHunterMediaReceiptVerifier : IHunterMediaReceiptVerifier
{
    public bool IsConfigured => false;
    public Task<VerifiedHunterMediaReceipt?> VerifyAsync(string opaqueReceipt, string ownerUserId,
        Guid submissionId, Guid captureSessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<VerifiedHunterMediaReceipt?>(null);
    }
}
