
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Trusted backend contract for Hunter status transitions.
///
/// Production implementations MUST atomically:
/// 1. Check the expected current submission status.
/// 2. Update the submission to the next status.
/// 3. Revoke its unconsumed capture sessions.
///
/// These operations must use a consistent transaction
/// and concurrency boundary.
///
/// A false result means the submission was not found
/// or its current status did not match expectedStatus.
///
/// Never implement this by independently saving the
/// status and then revoking sessions without rollback
/// and concurrency protection.
/// </summary>
public interface IHunterSubmissionStatusTransitionStore
{
    Task<bool> TryTransitionAndRevokeCaptureSessionsAsync(
        Guid submissionId,
        HunterSubmissionStatus expectedStatus,
        HunterSubmissionStatus nextStatus,
        CancellationToken cancellationToken = default);
}
