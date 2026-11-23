
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// SQL Server persistence for Hunter capture sessions.
/// Requires approved M4 EF mappings and SQL schema.
///
/// Not registered for production use yet.
/// </summary>
public sealed class SqlHunterCaptureSessionStore :
    IHunterCaptureSessionStore,
    IHunterCaptureSessionRevocationStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlHunterCaptureSessionStore(
        WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(
            nameof(db));
    }

    public async Task<Guid> CreateAsync(
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
                "Valid Hunter and submission IDs are required.");
        }

        if (issuedAtUtc == default ||
            expiresAtUtc == default ||
            issuedAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc <= issuedAtUtc)
        {
            throw new ArgumentException(
                "Valid UTC session timestamps are required.");
        }

        var sessionId = Guid.NewGuid();
        var strategy = _db.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
                _db.ChangeTracker.Clear();

            // A retry after an ambiguous commit must return the original
            // session instead of creating another capture grant.
            var prior = await _db.Set<HunterCaptureSessionRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (prior is not null)
            {
                if (prior.HunterId == hunterId &&
                    prior.HunterSubmissionId == hunterSubmissionId &&
                    prior.IssuedAtUtc == issuedAtUtc &&
                    prior.ExpiresAtUtc == expiresAtUtc)
                    return sessionId;

                throw new InvalidOperationException(
                    "Capture session retry did not match the original operation.");
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            // Lock the submission while checking eligibility
            // and inserting the new capture session.
            var eligible = await IsDraftOwnerLockedAsync(
                hunterId,
                hunterSubmissionId,
                cancellationToken);

            if (!eligible)
            {
                throw new UnauthorizedAccessException(
                    "Hunter capture session access denied.");
            }

            _db.Set<HunterCaptureSessionRow>().Add(
                new HunterCaptureSessionRow
                {
                    Id = sessionId,
                    HunterId = hunterId,
                    HunterSubmissionId = hunterSubmissionId,
                    IssuedAtUtc = issuedAtUtc,
                    ExpiresAtUtc = expiresAtUtc,
                    IsConsumed = false,
                    IsRevoked = false
                });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return sessionId;
        });
    }

    public async Task<bool> TryConsumeAsync(
        Guid captureSessionId,
        Guid authenticatedHunterId,
        Guid hunterSubmissionId,
        DateTimeOffset serverReceivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (captureSessionId == Guid.Empty ||
            authenticatedHunterId == Guid.Empty ||
            hunterSubmissionId == Guid.Empty ||
            serverReceivedAtUtc == default ||
            serverReceivedAtUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
                _db.ChangeTracker.Clear();

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var eligible = await IsDraftOwnerLockedAsync(
                authenticatedHunterId,
                hunterSubmissionId,
                cancellationToken);
            if (!eligible)
                return false;

            var updated = await _db.Set<HunterCaptureSessionRow>()
                .Where(x =>
                    x.Id == captureSessionId &&
                    x.HunterId == authenticatedHunterId &&
                    x.HunterSubmissionId == hunterSubmissionId &&
                    !x.IsConsumed &&
                    !x.IsRevoked &&
                    x.IssuedAtUtc <= serverReceivedAtUtc &&
                    x.ExpiresAtUtc > serverReceivedAtUtc)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.IsConsumed, true),
                    cancellationToken);
            if (updated != 1)
                return false;

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task<int> RevokeBySubmissionIdAsync(
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

        return await _db.Set<HunterCaptureSessionRow>()
            .Where(x =>
                x.HunterSubmissionId == hunterSubmissionId &&
                !x.IsConsumed &&
                !x.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsRevoked, true),
                cancellationToken);
    }

    public async Task<int> RevokeByHunterIdAsync(
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

        return await _db.Set<HunterCaptureSessionRow>()
            .Where(x =>
                x.HunterId == hunterId &&
                !x.IsConsumed &&
                !x.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsRevoked, true),
                cancellationToken);
    }

    private async Task<bool> IsDraftOwnerLockedAsync(
        Guid hunterId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        // SQL Server row lock. Caller must hold a
        // transaction during the complete operation.
        var submission = await _db
            .Set<HunterSubmission>()
            .FromSqlInterpolated(
                $"SELECT * FROM [Hunter_Submissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {submissionId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return submission is not null &&
            submission.Status == HunterSubmissionStatus.Draft &&
            Guid.TryParseExact(
                submission.UserId,
                "D",
                out var ownerId) &&
            ownerId == hunterId;
    }
}
