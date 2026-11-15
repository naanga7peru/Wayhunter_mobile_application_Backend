
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Server-side revocation contract for Hunter capture sessions.
///
/// Production implementations must coordinate revocation
/// with submission/account state changes transactionally.
/// Never accept these IDs directly from an untrusted client.
/// </summary>
public interface IHunterCaptureSessionRevocationStore
{
    // Revoke all unconsumed sessions for one submission.
    Task<int> RevokeBySubmissionIdAsync(
        Guid hunterSubmissionId,
        CancellationToken cancellationToken = default);

    // Revoke all unconsumed sessions belonging to one Hunter.
    Task<int> RevokeByHunterIdAsync(
        Guid hunterId,
        CancellationToken cancellationToken = default);
}
