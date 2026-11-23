using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

public interface IHunterMediaReceiptVerifier
{
    bool IsConfigured { get; }
    Task<VerifiedHunterMediaReceipt?> VerifyAsync(string opaqueReceipt, string ownerUserId,
        Guid submissionId, Guid captureSessionId, CancellationToken cancellationToken = default);
}
