
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// SQL-backed reader for trusted Hunter submission data.
/// Reads ownership and status from backend persistence.
/// Does not accept client-provided owner information.
/// </summary>
public sealed class SqlHunterCaptureSubmissionAccessReader
    : IHunterCaptureSubmissionAccessReader, IHunterOwnedSubmissionReader
{
    private readonly WayhunterSharedDbContext _dbContext;

    public SqlHunterCaptureSubmissionAccessReader(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(
                nameof(dbContext));
    }

    public async Task<HunterCaptureSubmissionAccessSnapshot?>
        FindBySubmissionIdAsync(
            Guid submissionId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext
            .Set<HunterSubmission>()
            .AsNoTracking()
            .Where(x => x.Id == submissionId)
            .Select(x =>
                new HunterCaptureSubmissionAccessSnapshot
                {
                    SubmissionId = x.Id,
                    OwnerUserId = x.UserId,
                    Status = x.Status
                })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<HunterSubmissionResponseDto?> GetMySubmissionAsync(
        string authenticatedUserId, Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        return QueryOwnedSubmissions(authenticatedUserId)
            .Where(submission => submission.Id == submissionId)
            .Select(SubmissionProjection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HunterSubmissionResponseDto>> ListMySubmissionsAsync(
        string authenticatedUserId, CancellationToken cancellationToken = default)
    {
        return await QueryOwnedSubmissions(authenticatedUserId)
            .OrderByDescending(submission => submission.CreatedAtUtc)
            .ThenBy(submission => submission.Id)
            .Select(SubmissionProjection)
            .ToListAsync(cancellationToken);
    }

    public async Task<HunterSubmissionResponseDto> AddDraftAsync(
        HunterSubmission draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Status != HunterSubmissionStatus.Draft ||
            draft.SubmittedAtUtc.HasValue)
        {
            throw new ArgumentException("Only a new Draft can be inserted.", nameof(draft));
        }

        _dbContext.HunterSubmissions.Add(draft);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new HunterSubmissionResponseDto
        {
            SubmissionId = draft.Id,
            PlaceName = draft.PlaceName,
            District = draft.District,
            Status = draft.Status,
            CreatedAtUtc = draft.CreatedAtUtc
        };
    }

    public async Task<bool> UpdateDraftDetailsAsync(
        string authenticatedUserId, Guid submissionId, string placeName,
        string? description, CancellationToken cancellationToken = default)
    {
        var affected = await QueryOwnedSubmissions(authenticatedUserId)
            .Where(submission => submission.Id == submissionId &&
                submission.Status == HunterSubmissionStatus.Draft &&
                submission.SubmittedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(submission => submission.PlaceName, placeName)
                .SetProperty(submission => submission.Description, description), cancellationToken);

        return affected == 1;
    }

    public Task<int> CountMyApprovedHuntsAsync(
        string authenticatedUserId, CancellationToken cancellationToken = default)
    {
        return QueryOwnedSubmissions(authenticatedUserId).CountAsync(
            submission => submission.Status ==
                HunterSubmissionStatus.Approved,
            cancellationToken);
    }

    public async Task<IReadOnlyList<HunterSubmissionResponseDto>> ListPendingReviewCandidatesAsync(
        int offset, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return await PendingReviewCandidates()
            .OrderBy(submission => submission.SubmittedAtUtc)
            .ThenBy(submission => submission.Id)
            .Skip(offset)
            .Take(limit)
            .Select(SubmissionProjection)
            .ToListAsync(cancellationToken);
    }

    public Task<HunterSubmissionResponseDto?> FindPendingReviewCandidateAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        PendingReviewCandidates().Where(submission => submission.Id == id)
            .Select(SubmissionProjection)
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<HunterSubmission> PendingReviewCandidates() =>
        _dbContext.HunterSubmissions.AsNoTracking().Where(submission =>
            submission.Status == HunterSubmissionStatus.PendingReview && submission.SubmittedAtUtc != null);

    private IQueryable<HunterSubmission> QueryOwnedSubmissions(string authenticatedUserId)
    {
        if (!Guid.TryParseExact(authenticatedUserId, "D", out var ownerId) ||
            ownerId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authenticated Hunter identity is required.");
        }

        var canonicalUserId = ownerId.ToString("D");
        return _dbContext.Set<HunterSubmission>()
            .AsNoTracking()
            .Where(submission => submission.UserId == canonicalUserId);
    }

    private static readonly System.Linq.Expressions.Expression<Func<HunterSubmission, HunterSubmissionResponseDto>>
        SubmissionProjection = submission => new HunterSubmissionResponseDto
        {
            SubmissionId = submission.Id,
            PlaceName = submission.PlaceName,
            District = submission.District,
            Status = submission.Status,
            CreatedAtUtc = submission.CreatedAtUtc,
            SubmittedAtUtc = submission.SubmittedAtUtc
        };
}
