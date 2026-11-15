
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Reads trusted Hunter submission ownership data
/// from backend-controlled storage.
///
/// Implementations must not trust client-provided owner IDs.
/// </summary>
public interface IHunterCaptureSubmissionAccessReader
{
    /// <summary>
    /// Returns a submission snapshot by its unique ID.
    /// Returns null when the submission does not exist.
    /// </summary>
    Task<HunterCaptureSubmissionAccessSnapshot?> FindBySubmissionIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);
}
