
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// In-memory prototype for reading trusted Hunter
/// submission ownership information.
///
/// Not a production database implementation.
/// The submissions must come from backend-controlled data.
/// </summary>
public sealed class InMemoryHunterCaptureSubmissionAccessReader
    : IHunterCaptureSubmissionAccessReader
{
    private readonly IReadOnlyDictionary<
        Guid,
        HunterCaptureSubmissionAccessSnapshot> _submissions;

    public InMemoryHunterCaptureSubmissionAccessReader(
        IEnumerable<HunterSubmission> submissions)
    {
        ArgumentNullException.ThrowIfNull(submissions);

        var snapshots = new Dictionary<
            Guid,
            HunterCaptureSubmissionAccessSnapshot>();

        foreach (var submission in submissions)
        {
            if (submission is null)
            {
                throw new ArgumentException(
                    "Submission collection cannot contain null items.",
                    nameof(submissions));
            }

            if (submission.Id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Submission ID cannot be empty.",
                    nameof(submissions));
            }

            var snapshot =
                new HunterCaptureSubmissionAccessSnapshot
                {
                    SubmissionId = submission.Id,
                    OwnerUserId = submission.UserId,
                    Status = submission.Status
                };

            if (!snapshots.TryAdd(submission.Id, snapshot))
            {
                throw new ArgumentException(
                    "Duplicate Hunter submission IDs are not allowed.",
                    nameof(submissions));
            }
        }

        // Keep a copy of the supplied data rather than
        // retaining mutable HunterSubmission entities.
        _submissions = snapshots;
    }

    public Task<HunterCaptureSubmissionAccessSnapshot?>
        FindBySubmissionIdAsync(
            Guid submissionId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            return Task.FromResult<
                HunterCaptureSubmissionAccessSnapshot?>(null);
        }

        _submissions.TryGetValue(
            submissionId,
            out var snapshot);

        return Task.FromResult(snapshot);
    }
}
