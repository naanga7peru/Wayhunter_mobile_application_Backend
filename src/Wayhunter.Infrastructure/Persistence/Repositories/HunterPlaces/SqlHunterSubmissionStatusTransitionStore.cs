
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Wayhunter.Application.Modules.HunterPlaces.Services;
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

    public Task<bool>
        TryTransitionAndRevokeCaptureSessionsAsync(
            Guid submissionId,
            HunterSubmissionStatus expectedStatus,
            HunterSubmissionStatus nextStatus,
            CancellationToken cancellationToken = default)
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

        return _db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            TransitionAsync(submissionId, expectedStatus, nextStatus, null, cancellationToken));
    }

    // Caller must enforce the approved ADM-04 permission before invoking this write.
    public Task<bool> TryRecordReviewAsync(
        Guid decisionId, string reviewerUserId, HunterReviewRequestDto request,
        DateTimeOffset reviewedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (decisionId == Guid.Empty || reviewedAtUtc == default || reviewedAtUtc.Offset != TimeSpan.Zero ||
            !Guid.TryParseExact(reviewerUserId, "D", out var reviewer) || reviewer == Guid.Empty ||
            HunterReviewRequestValidator.Validate(request).Count != 0 || request.Reason?.Length > 2000)
        {
            throw new ArgumentException("Invalid Hunter review decision.");
        }

        var decision = new HunterReviewDecision
        {
            Id = decisionId,
            HunterSubmissionId = request.HunterSubmissionId,
            ReviewerUserId = reviewer.ToString("D"),
            ResultingStatus = request.ResultingStatus,
            Reason = request.Reason?.Trim(),
            ReviewedAtUtc = reviewedAtUtc
        };
        return _db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            TransitionAsync(decision.HunterSubmissionId, HunterSubmissionStatus.PendingReview,
                decision.ResultingStatus, decision, cancellationToken));
    }

    private async Task<bool> TransitionAsync(
        Guid submissionId, HunterSubmissionStatus expectedStatus, HunterSubmissionStatus nextStatus,
        HunterReviewDecision? decision, CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Lock the submission row during the
        // complete status and revocation operation.
        var submission = await LockSubmissionAsync(submissionId, cancellationToken);

        // The decision ID also makes retries after an uncertain transaction commit safe.
        if (decision is not null)
        {
            var committed = await _db.HunterReviewDecisions
                .FromSqlInterpolated($"SELECT * FROM [Hunter_ReviewDecisions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {decision.Id}")
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == decision.Id, cancellationToken);
            if (committed is not null)
            {
                return committed.HunterSubmissionId == decision.HunterSubmissionId &&
                    committed.ReviewerUserId == decision.ReviewerUserId &&
                    committed.ResultingStatus == decision.ResultingStatus &&
                    committed.Reason == decision.Reason;
            }
        }

        if (submission is null ||
            submission.Status != expectedStatus)
        {
            return false;
        }

        // Update only when the expected status matches.
        var updated = await _db
            .Set<HunterSubmission>()
            .Where(x =>
                x.Id == submissionId &&
                x.Status == expectedStatus)
            .ExecuteUpdateAsync(
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
        await RevokeUnusedSessionsAsync(submissionId, cancellationToken);

        if (decision is not null)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [Hunter_ReviewDecisions]
                    ([Id], [HunterSubmissionId], [ReviewerUserId], [ResultingStatus], [Reason], [ReviewedAtUtc])
                VALUES ({decision.Id}, {decision.HunterSubmissionId}, {decision.ReviewerUserId},
                        {(int)decision.ResultingStatus}, {decision.Reason}, {decision.ReviewedAtUtc})
                """, cancellationToken);
        }

        // Both operations succeed together.
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    // Internal write primitive. Caller must supply provider-verified evidence and location.
    // An external replay key cannot be safely recorded using the current schema.
    public Task<bool> TrySubmitVerifiedDraftAsync(
        Guid submissionId, string ownerUserId, HunterSubmitRequestDto request,
        HunterVerifiedSubmitInputs verified, DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(verified);
        if (submissionId == Guid.Empty || !Guid.TryParseExact(ownerUserId, "D", out var owner) ||
            owner == Guid.Empty || submittedAtUtc == default || submittedAtUtc.Offset != TimeSpan.Zero ||
            HunterSubmitRequestValidator.Validate(request).Count != 0 ||
            string.IsNullOrWhiteSpace(verified.OwnerName) || verified.OwnerName.Length > 200 ||
            !string.Equals(verified.CanonicalDistrict, request.ClaimedDistrict.Trim(), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);

        HunterSubmissionEvidenceValidator.EnsureReady(submissionId, request, verified.Video, verified.Photo);
        return _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var draft = await LockSubmissionAsync(submissionId, cancellationToken);
            if (draft is null || draft.UserId != owner.ToString("D") ||
                draft.Status != HunterSubmissionStatus.Draft || draft.SubmittedAtUtc.HasValue ||
                submittedAtUtc < draft.CreatedAtUtc ||
                draft.District != verified.CanonicalDistrict || draft.Latitude != request.Latitude ||
                draft.Longitude != request.Longitude || draft.GpsAccuracyMeters != request.GpsAccuracyMeters ||
                draft.LocationCapturedAtUtc != request.LocationCapturedAtUtc ||
                draft.PlaceName != request.PlaceName.Trim() || draft.Description != request.Description?.Trim())
                return false;

            var video = await _db.HunterEvidence.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == verified.Video.Id, cancellationToken);
            var photo = await _db.HunterEvidence.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == verified.Photo.Id, cancellationToken);
            if (!MatchesVerifiedEvidence(video, verified.Video) || !MatchesVerifiedEvidence(photo, verified.Photo))
                return false;

            await _db.HunterSubmissions.Where(row => row.Id == submissionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, HunterSubmissionStatus.PendingReview)
                    .SetProperty(row => row.SubmittedAtUtc, submittedAtUtc)
                    .SetProperty(row => row.UserNameSnapshot, verified.OwnerName), cancellationToken);
            await RevokeUnusedSessionsAsync(submissionId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private static bool MatchesVerifiedEvidence(HunterEvidence? stored, HunterEvidence verified) =>
        stored is not null && stored.HunterSubmissionId == verified.HunterSubmissionId &&
        stored.EvidenceType == verified.EvidenceType && stored.PrivateMediaReference == verified.PrivateMediaReference &&
        stored.CapturedAtUtc == verified.CapturedAtUtc && stored.UploadedAtUtc == verified.UploadedAtUtc &&
        stored.CaptureLatitude == verified.CaptureLatitude && stored.CaptureLongitude == verified.CaptureLongitude &&
        stored.DurationSeconds == verified.DurationSeconds;

    private Task<HunterSubmission?> LockSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) =>
        _db.HunterSubmissions.FromSqlInterpolated(
            $"SELECT * FROM [Hunter_Submissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {submissionId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    private Task<int> RevokeUnusedSessionsAsync(Guid submissionId, CancellationToken cancellationToken) =>
        _db.Set<HunterCaptureSessionRow>().Where(row =>
                row.HunterSubmissionId == submissionId && !row.IsConsumed && !row.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.IsRevoked, true), cancellationToken);

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
