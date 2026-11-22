using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Provides the existing owner-scoped Hunter status read contract.
/// Callers must supply identity derived from trusted authentication.
/// Missing and foreign submissions are both concealed as null.
/// </summary>
public sealed class HunterSubmissionStatusService
{
    private readonly IHunterSubmissionStatusReader _reader;

    public HunterSubmissionStatusService(
        IHunterSubmissionStatusReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<HunterSubmissionResponseDto?> GetMySubmissionAsync(
        string? authenticatedUserId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetCanonicalId(authenticatedUserId, out var actorId) ||
            submissionId == Guid.Empty)
        {
            return null;
        }

        var snapshot = await _reader.FindByIdAsync(
            submissionId,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (snapshot is null ||
            snapshot.SubmissionId != submissionId ||
            !TryGetCanonicalId(snapshot.OwnerUserId, out var ownerId) ||
            ownerId != actorId)
        {
            return null;
        }

        return new HunterSubmissionResponseDto
        {
            SubmissionId = snapshot.SubmissionId,
            PlaceName = snapshot.PlaceName,
            District = snapshot.District,
            Status = snapshot.Status,
            CreatedAtUtc = snapshot.CreatedAtUtc,
            SubmittedAtUtc = snapshot.SubmittedAtUtc
        };
    }

    private static bool TryGetCanonicalId(
        string? value,
        out Guid id)
    {
        id = Guid.Empty;
        return Guid.TryParseExact(value, "D", out id) && id != Guid.Empty;
    }
}
