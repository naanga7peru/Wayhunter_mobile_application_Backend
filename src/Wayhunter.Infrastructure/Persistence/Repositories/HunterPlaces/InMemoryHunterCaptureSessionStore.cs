
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

public sealed class InMemoryHunterCaptureSessionStore
    : IHunterCaptureSessionStore
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
                IsConsumed = false
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
                captureSessionId, out var session))
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

            // Check and update happen under one lock.
            // Only one concurrent caller can consume this session.
            session.IsConsumed = true;

            return Task.FromResult(true);
        }
    }
}
