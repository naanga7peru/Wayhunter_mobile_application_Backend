using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// SQL-backed, read-only Hunter submission projection.
/// Not registered for runtime use until M4 identity/persistence approval.
/// </summary>
public sealed class SqlHunterSubmissionStatusReader
    : IHunterSubmissionStatusReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlHunterSubmissionStatusReader(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<HunterSubmissionStatusSnapshot?> FindByIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (submissionId == Guid.Empty)
        {
            return null;
        }

        return await _db.HunterSubmissions
            .AsNoTracking()
            .Where(x => x.Id == submissionId)
            .Select(x => new HunterSubmissionStatusSnapshot
            {
                SubmissionId = x.Id,
                OwnerUserId = x.UserId,
                PlaceName = x.PlaceName,
                District = x.District,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                SubmittedAtUtc = x.SubmittedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HunterSubmissionStatusSnapshot>> FindMineAsync(
        string ownerUserId,
        bool approvedOnly = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(ownerUserId, "D", out var ownerId) || ownerId == Guid.Empty)
            return Array.Empty<HunterSubmissionStatusSnapshot>();

        var query = _db.HunterSubmissions.AsNoTracking()
            .Where(x => x.UserId == ownerId.ToString("D"));
        if (approvedOnly)
            query = query.Where(x => x.Status == HunterSubmissionStatus.Approved);

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new HunterSubmissionStatusSnapshot
            {
                SubmissionId = x.Id,
                OwnerUserId = x.UserId,
                PlaceName = x.PlaceName,
                District = x.District,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                SubmittedAtUtc = x.SubmittedAtUtc
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovedHunterPlaceDto>> FindApprovedPlacesAsync(
        CancellationToken cancellationToken = default) =>
        await _db.HunterSubmissions.AsNoTracking()
            .Where(x => x.Status == HunterSubmissionStatus.Approved &&
                _db.HunterReviewDecisions.Any(review =>
                    review.HunterSubmissionId == x.Id &&
                    review.ResultingStatus == HunterSubmissionStatus.Approved))
            .OrderBy(x => x.District).ThenBy(x => x.PlaceName).ThenBy(x => x.Id)
            .Select(x => new ApprovedHunterPlaceDto(
                x.Id, x.PlaceName, x.District, x.Latitude, x.Longitude,
                _db.HunterReviewDecisions.Where(review =>
                        review.HunterSubmissionId == x.Id &&
                        review.ResultingStatus == HunterSubmissionStatus.Approved)
                    .Select(review => review.ReviewedAtUtc).First()))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<HunterSubmissionStatusSnapshot>> FindPendingReviewAsync(
        CancellationToken cancellationToken = default) =>
        await _db.HunterSubmissions.AsNoTracking()
            .Where(x => x.Status == HunterSubmissionStatus.PendingReview)
            .OrderBy(x => x.SubmittedAtUtc).ThenBy(x => x.Id)
            .Select(x => new HunterSubmissionStatusSnapshot
            {
                SubmissionId = x.Id,
                OwnerUserId = x.UserId,
                PlaceName = x.PlaceName,
                District = x.District,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                SubmittedAtUtc = x.SubmittedAtUtc
            })
            .ToArrayAsync(cancellationToken);
}
