
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
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            // Use the same submission lock order as consume and review transactions.
            var eligible = await IsDraftOwnerLockedAsync(hunterId, hunterSubmissionId, cancellationToken);
            var committed = await _db.HunterCaptureSessions.AsNoTracking().AnyAsync(
                row => row.Id == sessionId && row.HunterId == hunterId &&
                    row.HunterSubmissionId == hunterSubmissionId, cancellationToken);
            if (committed)
                return sessionId;

            if (!eligible)
                throw new UnauthorizedAccessException("Hunter capture session access denied.");

            var row = new HunterCaptureSessionRow
            {
                Id = sessionId,
                HunterId = hunterId,
                HunterSubmissionId = hunterSubmissionId,
                IssuedAtUtc = issuedAtUtc,
                ExpiresAtUtc = expiresAtUtc
            };

            var entry = _db.HunterCaptureSessions.Add(row);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return sessionId;
            }
            finally
            {
                entry.State = EntityState.Detached;
            }
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

        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(() => ConsumeAsync(
            captureSessionId, authenticatedHunterId, hunterSubmissionId, serverReceivedAtUtc, cancellationToken));
    }

    private async Task<bool> ConsumeAsync(
        Guid captureSessionId, Guid authenticatedHunterId, Guid hunterSubmissionId,
        DateTimeOffset serverReceivedAtUtc, CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Check trusted submission ownership and status.
        var eligible = await IsDraftOwnerLockedAsync(
            authenticatedHunterId,
            hunterSubmissionId,
            cancellationToken);

        if (!eligible)
        {
            return false;
        }

        // ExecuteUpdate performs one conditional SQL UPDATE.
        // Only one concurrent request can change the row
        // from unused to consumed.
        var updated = await _db
            .Set<HunterCaptureSessionRow>()
            .Where(x =>
                x.Id == captureSessionId &&
                x.HunterId == authenticatedHunterId &&
                x.HunterSubmissionId == hunterSubmissionId &&
                !x.IsConsumed &&
                !x.IsRevoked &&
                x.IssuedAtUtc <= serverReceivedAtUtc &&
                x.ExpiresAtUtc > serverReceivedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.IsConsumed,
                        true),
                cancellationToken);

        if (updated != 1)
        {
            return false;
        }

        await transaction.CommitAsync(cancellationToken);

        return true;
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

    public Task<bool> IsEligibleForEvidenceAsync(
        Guid sessionId, Guid hunterId, Guid submissionId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sessionId == Guid.Empty || hunterId == Guid.Empty || submissionId == Guid.Empty ||
            now == default || now.Offset != TimeSpan.Zero)
            return Task.FromResult(false);

        var owner = hunterId.ToString("D");
        return _db.HunterCaptureSessions.AnyAsync(session =>
            session.Id == sessionId && session.HunterId == hunterId &&
            session.HunterSubmissionId == submissionId && session.OwnerUserId == owner &&
            !session.IsConsumed && !session.IsRevoked &&
            session.IssuedAtUtc <= now && session.ExpiresAtUtc > now &&
            _db.HunterSubmissions.Any(submission => submission.Id == submissionId &&
                submission.UserId == owner && submission.Status == HunterSubmissionStatus.Draft),
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
