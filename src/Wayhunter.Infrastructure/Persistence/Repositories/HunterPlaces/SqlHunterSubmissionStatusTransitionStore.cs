
using System;
using System.Data;
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
/// SQL persistence for Hunter submission status changes.
///
/// Status update and capture-session revocation
/// must complete in one database transaction.
///
/// Production registration requires integration review.
/// </summary>
public sealed class SqlHunterSubmissionStatusTransitionStore
    : IHunterSubmissionStatusTransitionStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlHunterSubmissionStatusTransitionStore(
        WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(
            nameof(db));
    }

    public async Task<bool>
        TryTransitionAndRevokeCaptureSessionsAsync(
            Guid submissionId,
            HunterSubmissionStatus expectedStatus,
            HunterSubmissionStatus nextStatus,
            CancellationToken cancellationToken = default)
    {
        return await TransitionAndRevokeAsync(
            submissionId,
            expectedStatus,
            nextStatus,
            submittedAtUtc: null,
            cancellationToken);
    }

    public Task<bool> TrySubmitDraftAndRevokeCaptureSessionsAsync(
        Guid submissionId,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (submittedAtUtc == default ||
            submittedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A UTC submission time is required.",
                nameof(submittedAtUtc));
        }

        return TransitionAndRevokeAsync(
            submissionId,
            HunterSubmissionStatus.Draft,
            HunterSubmissionStatus.PendingReview,
            submittedAtUtc,
            cancellationToken);
    }

    private async Task<bool> TransitionAndRevokeAsync(
        Guid submissionId,
        HunterSubmissionStatus expectedStatus,
        HunterSubmissionStatus nextStatus,
        DateTimeOffset? submittedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(submissionId));
        }

        if (!IsAllowedTransition(
            expectedStatus,
            nextStatus))
        {
            throw new ArgumentException(
                "Unsupported Hunter status transition.",
                nameof(nextStatus));
        }

        if (expectedStatus == HunterSubmissionStatus.Draft &&
            nextStatus == HunterSubmissionStatus.PendingReview &&
            !submittedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Use the submission transition so SubmittedAtUtc is recorded.",
                nameof(nextStatus));
        }

        return await HunterSqlTransaction.ExecuteAsync(_db,
            () => TransitionCoreAsync(submissionId, expectedStatus, nextStatus, submittedAtUtc, cancellationToken), cancellationToken);
    }

    private async Task<bool> TransitionCoreAsync(Guid submissionId, HunterSubmissionStatus expectedStatus, HunterSubmissionStatus nextStatus,
        DateTimeOffset? submittedAtUtc, CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Lock the submission row during the
        // complete status and revocation operation.
        var submission = await _db
            .Set<HunterSubmission>()
            .FromSqlInterpolated(
                $"SELECT * FROM [Hunter_Submissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {submissionId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (submission is null ||
            submission.Status != expectedStatus)
        {
            return false;
        }

        // Update only when the expected status matches.
        var update = _db
            .Set<HunterSubmission>()
            .Where(x =>
                x.Id == submissionId &&
                x.Status == expectedStatus);

        var updated = submittedAtUtc.HasValue
            ? await update.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, nextStatus)
                    .SetProperty(
                        x => x.SubmittedAtUtc,
                        submittedAtUtc.Value),
                cancellationToken)
            : await update.ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.Status,
                    nextStatus),
                cancellationToken);

        if (updated != 1)
        {
            // Transaction disposal rolls back changes.
            return false;
        }

        // Revoke all unused sessions for
        // this submission in the same transaction.
        await _db
            .Set<HunterCaptureSessionRow>()
            .Where(x =>
                x.HunterSubmissionId == submissionId &&
                !x.IsConsumed &&
                !x.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.IsRevoked,
                    true),
                cancellationToken);

        // Both operations succeed together.
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private static bool IsAllowedTransition(
        HunterSubmissionStatus from,
        HunterSubmissionStatus to)
    {
        // Draft -> PendingReview
        if (from == HunterSubmissionStatus.Draft &&
            to == HunterSubmissionStatus.PendingReview)
        {
            return true;
        }

        // PendingReview -> Admin decision
        if (from == HunterSubmissionStatus.PendingReview &&
            to is (
                HunterSubmissionStatus.Held or
                HunterSubmissionStatus.Approved or
                HunterSubmissionStatus.Rejected))
        {
            return true;
        }

        return false;
    }
}
