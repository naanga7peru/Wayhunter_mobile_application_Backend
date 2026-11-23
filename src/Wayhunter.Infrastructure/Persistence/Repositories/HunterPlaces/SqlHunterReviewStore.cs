using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// SQL implementation of the atomic M4 review boundary.
/// Uses existing EF mappings only; production registration and SQL
/// execution remain gated by the M4/DB readiness decisions.
/// </summary>
public sealed class SqlHunterReviewStore : IHunterReviewStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlHunterReviewStore(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<HunterSubmissionResponseDto?> TryReviewPendingAsync(
        HunterReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateDecision(decision);

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var submission = await _db.HunterSubmissions
            .FromSqlInterpolated(
                $"SELECT * FROM [Hunter_Submissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {decision.HunterSubmissionId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (submission is null ||
            submission.Status != HunterSubmissionStatus.PendingReview)
        {
            return null;
        }

        if (!submission.SubmittedAtUtc.HasValue ||
            submission.SubmittedAtUtc.Value == default ||
            submission.SubmittedAtUtc.Value.Offset != TimeSpan.Zero ||
            decision.ReviewedAtUtc < submission.SubmittedAtUtc.Value)
        {
            throw new InvalidOperationException(
                "Hunter submission timestamps are invalid for review.");
        }

        if (decision.ResultingStatus == HunterSubmissionStatus.Approved)
        {
            var verified = await _db.HunterEvidence.AsNoTracking()
                .Where(x => x.HunterSubmissionId == decision.HunterSubmissionId &&
                            x.OwnerUserId == submission.UserId &&
                            x.ProviderVerifiedAtUtc != null &&
                            x.CaptureSessionId != null &&
                            x.ProviderAssetIdHash != null &&
                            x.ProviderReceiptDigest != null)
                .OrderBy(x => x.UploadedAtUtc)
                .ToListAsync(cancellationToken);
            var sessionIds = verified.Where(x => x.CaptureSessionId.HasValue)
                .Select(x => x.CaptureSessionId!.Value).ToArray();
            var boundSessions = await _db.HunterCaptureSessions.AsNoTracking()
                .Where(x => sessionIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            bool ownerIdentityValid = Guid.TryParseExact(submission.UserId, "D", out var ownerHunterId);
            if (verified.Count != 2 ||
                verified[0].EvidenceType != HunterEvidenceType.Guided360Video ||
                verified[1].EvidenceType != HunterEvidenceType.LivePhoto ||
                verified[0].UploadedAtUtc >= verified[1].CapturedAtUtc ||
                verified.Any(x => x.CapturedAtUtc > x.UploadedAtUtc) ||
                !ownerIdentityValid || boundSessions.Count != 2 ||
                verified.Any(e => !boundSessions.TryGetValue(e.CaptureSessionId!.Value, out var session) ||
                    session.HunterSubmissionId != decision.HunterSubmissionId ||
                    session.HunterId != ownerHunterId || session.OwnerUserId != submission.UserId ||
                    !session.IsConsumed || session.IsRevoked ||
                    e.CapturedAtUtc < session.IssuedAtUtc || e.CapturedAtUtc >= session.ExpiresAtUtc))
            {
                throw new InvalidOperationException(
                    "A Hunter submission cannot be approved without complete verified evidence.");
            }
        }

        var updated = await _db.HunterSubmissions
            .Where(x => x.Id == decision.HunterSubmissionId &&
                        x.Status == HunterSubmissionStatus.PendingReview)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.Status,
                    decision.ResultingStatus),
                cancellationToken);

        if (updated != 1)
        {
            return null;
        }

        _db.HunterReviewDecisions.Add(decision);

        await _db.HunterCaptureSessions
            .Where(x => x.HunterSubmissionId == decision.HunterSubmissionId &&
                        !x.IsConsumed &&
                        !x.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsRevoked, true),
                cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(submission, decision.ResultingStatus);
    }

    private static void ValidateDecision(HunterReviewDecision? decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Id == Guid.Empty ||
            decision.HunterSubmissionId == Guid.Empty ||
            !Guid.TryParseExact(decision.ReviewerUserId, "D", out var reviewerId) ||
            reviewerId == Guid.Empty ||
            decision.ReviewedAtUtc == default ||
            decision.ReviewedAtUtc.Offset != TimeSpan.Zero ||
            decision.Reason?.Length > 2000 ||
            decision.ResultingStatus is not (HunterSubmissionStatus.Approved or HunterSubmissionStatus.Rejected) ||
            (decision.ResultingStatus == HunterSubmissionStatus.Rejected &&
             string.IsNullOrWhiteSpace(decision.Reason)))
        {
            throw new ArgumentException(
                "Hunter review decision is invalid.",
                nameof(decision));
        }
    }

    private static HunterSubmissionResponseDto ToResponse(
        HunterSubmission submission,
        HunterSubmissionStatus resultingStatus)
    {
        return new HunterSubmissionResponseDto
        {
            SubmissionId = submission.Id,
            PlaceName = submission.PlaceName,
            District = submission.District,
            Status = resultingStatus,
            CreatedAtUtc = submission.CreatedAtUtc,
            SubmittedAtUtc = submission.SubmittedAtUtc
        };
    }
}
