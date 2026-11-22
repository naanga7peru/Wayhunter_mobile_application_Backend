using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Reads a submission and its owner from authoritative storage.
/// </summary>
public interface IHunterSubmissionStatusReader
{
    Task<HunterSubmissionStatusSnapshot?> FindByIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);
}
