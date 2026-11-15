
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// In-memory Hunter capture-session prototype.
/// Not a production database implementation.
///
/// Session consumption and revocation are synchronized
/// by the same lock.
/// </summary>
public sealed class InMemoryHunterCaptureSessionStore
    : IHunterCaptureSessionStore,
      IHunterCaptureSessionRevocationStore
{
    private readonly object _gate = new();

    private readonly Dictionary<Guid, CaptureSession>
        _sessions = new();

    private sealed class CaptureSession
    {
        public required Guid HunterId { get; init; }

        public required Guid SubmissionId { get; init; }

        public required DateTimeOffset IssuedAtUtc { get; init; }

        public required DateTimeOffset ExpiresAtUtc { get; init; }

        public bool IsConsumed { get; set; }

        public bool IsRevoked { get; set; }
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
                "A valid UTC session time range is required.");
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                ExpiresAtUtc = expiresAtUtc,
                IsConsumed = false,
                IsRevoked = false
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

            if (!_sessions.TryGetValue(
                    captureSessionId,
                    out var session))
            {
                return Task.FromResult(false);
            }

            // Revoked sessions must never be consumed.
            if (session.IsRevoked)
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

            // Validation and consumption are atomic
            // relative to revocation in this store.
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

            var revokedCount = 0;

            foreach (var session in _sessions.Values)
            {
                if (session.SubmissionId != hunterSubmissionId ||
                    session.IsConsumed ||
                    session.IsRevoked)
                {
                    continue;
                }

                session.IsRevoked = true;
                revokedCount++;
            }

            return Task.FromResult(revokedCount);
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

            var revokedCount = 0;

            foreach (var session in _sessions.Values)
            {
                if (session.HunterId != hunterId ||
                    session.IsConsumed ||
                    session.IsRevoked)
                {
                    continue;
                }

                session.IsRevoked = true;
                revokedCount++;
            }

            return Task.FromResult(revokedCount);
        }
    }
}
