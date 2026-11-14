
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

public sealed class HunterCaptureSessionService
{
    private static readonly TimeSpan SessionLifetime =
        TimeSpan.FromMinutes(10);

    private readonly IHunterCaptureSessionStore _store;
    private readonly TimeProvider _timeProvider;

    public HunterCaptureSessionService(
        IHunterCaptureSessionStore store,
        TimeProvider timeProvider)
    {
        _store = store
            ?? throw new ArgumentNullException(nameof(store));

        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Guid> StartAsync(
        Guid authenticatedHunterId,
        Guid hunterSubmissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (authenticatedHunterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Authenticated Hunter ID is required.",
                nameof(authenticatedHunterId));
        }

        if (hunterSubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(hunterSubmissionId));
        }

        // Use backend time, never client-supplied time.
        var issuedAtUtc = _timeProvider.GetUtcNow();
        var expiresAtUtc = issuedAtUtc.Add(SessionLifetime);

        return await _store.CreateAsync(
            authenticatedHunterId,
            hunterSubmissionId,
            issuedAtUtc,
            expiresAtUtc,
            cancellationToken);
    }

    public Task<bool> TryConsumeAsync(
        Guid captureSessionId,
        Guid authenticatedHunterId,
        Guid hunterSubmissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (captureSessionId == Guid.Empty ||
            authenticatedHunterId == Guid.Empty ||
            hunterSubmissionId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        // Server timestamp prevents client clock manipulation.
        var serverReceivedAtUtc = _timeProvider.GetUtcNow();

        return _store.TryConsumeAsync(
            captureSessionId,
            authenticatedHunterId,
            hunterSubmissionId,
            serverReceivedAtUtc,
            cancellationToken);
    }
}
