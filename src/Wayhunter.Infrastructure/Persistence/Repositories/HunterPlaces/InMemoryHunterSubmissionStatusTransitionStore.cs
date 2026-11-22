
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// Integrated in-memory prototype for submission status
/// and capture sessions. NOT for production.
///
/// For tests, use the SAME instance for status transitions,
/// capture issuance, consumption, revocation and access reads.
/// A single lock protects all these operations.
/// </summary>
public sealed class InMemoryHunterSubmissionStatusTransitionStore :
    IHunterSubmissionStatusTransitionStore,
    IHunterCaptureSessionStore,
    IHunterCaptureSessionRevocationStore,
    IHunterCaptureSubmissionAccessReader
{
    private readonly object _gate = new();

    private readonly Dictionary<Guid,
        HunterCaptureSubmissionAccessSnapshot> _submissions = new();

    private readonly Dictionary<Guid, CaptureSession> _sessions = new();

    private sealed class CaptureSession
    {
        public required Guid HunterId { get; init; }
        public required Guid SubmissionId { get; init; }
        public required DateTimeOffset IssuedAtUtc { get; init; }
        public required DateTimeOffset ExpiresAtUtc { get; init; }
        public bool IsConsumed { get; set; }
        public bool IsRevoked { get; set; }
    }

    public InMemoryHunterSubmissionStatusTransitionStore(
        IEnumerable<HunterSubmission> submissions)
    {
        ArgumentNullException.ThrowIfNull(submissions);

        foreach (var submission in submissions)
        {
            if (submission is null ||
                submission.Id == Guid.Empty ||
                !Guid.TryParse(submission.UserId, out _) ||
                !Enum.IsDefined(submission.Status))
            {
                throw new ArgumentException(
                    "A valid trusted Hunter submission is required.",
                    nameof(submissions));
            }

            if (!_submissions.TryAdd(
                submission.Id,
                new HunterCaptureSubmissionAccessSnapshot
                {
                    SubmissionId = submission.Id,
                    OwnerUserId = submission.UserId,
                    Status = submission.Status,
                    SubmittedAtUtc = submission.SubmittedAtUtc
                }))
            {
                throw new ArgumentException(
                    "Duplicate submission ID.",
                    nameof(submissions));
            }
        }
    }

    public Task<HunterCaptureSubmissionAccessSnapshot?>
        FindBySubmissionIdAsync(
            Guid submissionId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _submissions.TryGetValue(submissionId, out var snapshot);

            return Task.FromResult(snapshot);
        }
    }

    public Task<bool> TrySubmitDraftAndRevokeCaptureSessionsAsync(
        Guid submissionId,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty ||
            submittedAtUtc == default ||
            submittedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A submission ID and UTC submission time are required.");
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_submissions.TryGetValue(submissionId, out var snapshot) ||
                snapshot.Status != HunterSubmissionStatus.Draft)
            {
                return Task.FromResult(false);
            }

            _submissions[submissionId] = snapshot with
            {
                Status = HunterSubmissionStatus.PendingReview,
                SubmittedAtUtc = submittedAtUtc
            };
            RevokeSubmissionCore(submissionId);
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryTransitionAndRevokeCaptureSessionsAsync(
        Guid submissionId,
        HunterSubmissionStatus expectedStatus,
        HunterSubmissionStatus nextStatus,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Submission ID is required.",
                nameof(submissionId));
        }

        if (!IsAllowedTransition(expectedStatus, nextStatus))
        {
            throw new ArgumentException(
                "Unsupported Hunter status transition.",
                nameof(nextStatus));
        }

        if (expectedStatus == HunterSubmissionStatus.Draft &&
            nextStatus == HunterSubmissionStatus.PendingReview)
        {
            throw new ArgumentException(
                "Use the submission transition so SubmittedAtUtc is recorded.",
                nameof(nextStatus));
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_submissions.TryGetValue(
                    submissionId,
                    out var snapshot) ||
                snapshot.Status != expectedStatus)
            {
                return Task.FromResult(false);
            }

            // No await or external storage call inside this lock.
            // The status update and revocation are performed
            // under one synchronization boundary.
            _submissions[submissionId] = snapshot with
            {
                Status = nextStatus
            };

            RevokeSubmissionCore(submissionId);

            return Task.FromResult(true);
        }
    }

    public Task<Guid> CreateAsync(
        Guid hunterId,
        Guid hunterSubmissionId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (hunterId == Guid.Empty ||
            hunterSubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter and submission IDs are required.");
        }

        if (issuedAtUtc == default ||
            expiresAtUtc == default ||
            issuedAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc <= issuedAtUtc)
        {
            throw new ArgumentException(
                "Valid UTC capture times are required.");
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsEligibleDraftOwner(hunterId, hunterSubmissionId))
            {
                throw new UnauthorizedAccessException(
                    "Hunter capture session access denied.");
            }

            Guid sessionId;

            do
            {
                sessionId = Guid.NewGuid();
            }
            while (_sessions.ContainsKey(sessionId));

            _sessions.Add(sessionId, new CaptureSession
            {
                HunterId = hunterId,
                SubmissionId = hunterSubmissionId,
                IssuedAtUtc = issuedAtUtc,
                ExpiresAtUtc = expiresAtUtc
            });

            return Task.FromResult(sessionId);
        }
    }

    public Task<bool> TryConsumeAsync(
        Guid captureSessionId,
        Guid authenticatedHunterId,
        Guid hunterSubmissionId,
        DateTimeOffset serverReceivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (captureSessionId == Guid.Empty ||
            authenticatedHunterId == Guid.Empty ||
            hunterSubmissionId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsEligibleDraftOwner(
                    authenticatedHunterId,
                    hunterSubmissionId) ||
                !_sessions.TryGetValue(
                    captureSessionId,
                    out var session) ||
                session.IsRevoked)
            {
                return Task.FromResult(false);
            }

            try
            {
                HunterCaptureSessionRules.Validate(
                    authenticatedHunterId,
                    session.HunterId,
                    hunterSubmissionId,
                    session.SubmissionId,
                    session.IssuedAtUtc,
                    session.ExpiresAtUtc,
                    serverReceivedAtUtc,
                    session.IsConsumed);
            }
            catch (ArgumentException)
            {
                return Task.FromResult(false);
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(false);
            }

            session.IsConsumed = true;

            return Task.FromResult(true);
        }
    }

    public Task<int> RevokeBySubmissionIdAsync(
        Guid hunterSubmissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (hunterSubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Submission ID is required.",
                nameof(hunterSubmissionId));
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                RevokeSubmissionCore(hunterSubmissionId));
        }
    }

    public Task<int> RevokeByHunterIdAsync(
        Guid hunterId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (hunterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter ID is required.",
                nameof(hunterId));
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var count = 0;

            foreach (var session in _sessions.Values)
            {
                if (session.HunterId != hunterId ||
                    session.IsConsumed ||
                    session.IsRevoked)
                {
                    continue;
                }

                session.IsRevoked = true;
                count++;
            }

            return Task.FromResult(count);
        }
    }

    private int RevokeSubmissionCore(Guid submissionId)
    {
        // Only call while holding _gate.
        var count = 0;

        foreach (var session in _sessions.Values)
        {
            if (session.SubmissionId != submissionId ||
                session.IsConsumed ||
                session.IsRevoked)
            {
                continue;
            }

            session.IsRevoked = true;
            count++;
        }

        return count;
    }

    private bool IsEligibleDraftOwner(
        Guid hunterId,
        Guid submissionId)
    {
        // Only call while holding _gate.
        return _submissions.TryGetValue(
                   submissionId,
                   out var snapshot) &&
               snapshot.Status == HunterSubmissionStatus.Draft &&
               Guid.TryParse(snapshot.OwnerUserId, out var ownerId) &&
               ownerId == hunterId;
    }

    private static bool IsAllowedTransition(
        HunterSubmissionStatus from,
        HunterSubmissionStatus to)
    {
        return (from == HunterSubmissionStatus.Draft &&
                to == HunterSubmissionStatus.PendingReview) ||
               (from == HunterSubmissionStatus.PendingReview &&
                to is
                    HunterSubmissionStatus.Held
                    or HunterSubmissionStatus.Approved
                    or HunterSubmissionStatus.Rejected);
    }
}
