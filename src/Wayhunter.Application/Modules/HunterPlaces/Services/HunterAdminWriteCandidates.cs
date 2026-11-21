using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

public sealed record HunterPrivateEvidenceAccess(Uri AccessUrl, DateTimeOffset ExpiresAtUtc);

// Adapter must enforce review/evidence binding and issue a short-lived private URL.
public interface IHunterPrivateEvidenceCandidate
{
    Task<HunterPrivateEvidenceAccess?> AuthorizeAsync(
        string reviewer, Guid submissionId, Guid evidenceId, CancellationToken ct);
}

// Adapter must condition on active publication and persist actor, timestamp and changes atomically.
public interface IHunterPlaceModerationCandidate
{
    Task<bool> UpdateAsync(string reviewer, Guid placeId, string name, string? description, CancellationToken ct);
    Task<bool> DeactivateAsync(string reviewer, Guid placeId, string reason, CancellationToken ct);
}
