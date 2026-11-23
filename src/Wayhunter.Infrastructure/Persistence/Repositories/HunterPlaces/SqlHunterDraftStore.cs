using System.Data;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Validators;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

public sealed class SqlHunterDraftStore : IHunterDraftStore
{
    private readonly WayhunterSharedDbContext _db;
    public SqlHunterDraftStore(WayhunterSharedDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<HunterSubmission> CreateAsync(HunterSubmission submission, string keyHash, string requestDigest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        cancellationToken.ThrowIfCancellationRequested();
        if (submission.Id == Guid.Empty || !Guid.TryParseExact(submission.UserId, "D", out _) ||
            submission.Status != HunterSubmissionStatus.Draft || submission.CreatedAtUtc == default ||
            submission.CreatedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Draft submission is invalid.", nameof(submission));
        if (keyHash.Length != 64 || requestDigest.Length != 64)
            throw new ArgumentException("Idempotency hashes must be SHA-256.");
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await _db.HunterRequestIdempotency
            .FromSqlInterpolated($"SELECT * FROM [Hunter_RequestIdempotency] WITH (UPDLOCK, HOLDLOCK) WHERE [OwnerUserId] = {submission.UserId} AND [Operation] = {"draft.create"} AND [KeyHash] = {keyHash}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestDigest != requestDigest)
                throw new HunterIdempotencyConflictException();
            var prior = await _db.HunterSubmissions.SingleOrDefaultAsync(
                x => x.Id == existing.ResourceId && x.UserId == submission.UserId, cancellationToken);
            if (prior is null) throw new InvalidOperationException("Idempotent draft result no longer exists.");
            await tx.CommitAsync(cancellationToken);
            return prior;
        }
        await _db.HunterSubmissions.AddAsync(submission, cancellationToken);
        await _db.HunterRequestIdempotency.AddAsync(new HunterRequestIdempotencyRow
        {
            Id = Guid.NewGuid(), OwnerUserId = submission.UserId, Operation = "draft.create",
            KeyHash = keyHash, RequestDigest = requestDigest, ResourceId = submission.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return submission;
    }

    public async Task<HunterDraftUpdateResult> UpdateDraftAsync(Guid submissionId, string ownerUserId,
        HunterDraftRequestDto request, CancellationToken cancellationToken = default)
    {
        if (submissionId == Guid.Empty || !Guid.TryParseExact(ownerUserId, "D", out _)) return HunterDraftUpdateResult.NotFound;
        var now = DateTimeOffset.UtcNow;
        var errors = HunterDraftRequestValidator.Validate(request, now);
        if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors), nameof(request));

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var row = await _db.HunterSubmissions
            .FromSqlInterpolated($"SELECT * FROM [Hunter_Submissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {submissionId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null || row.UserId != ownerUserId) return HunterDraftUpdateResult.NotFound;
        if (row.Status != HunterSubmissionStatus.Draft) return HunterDraftUpdateResult.Conflict;
        HunterDraftRequestValidator.Apply(request, row);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return HunterDraftUpdateResult.Updated;
    }

    public async Task<HunterDraftDeleteResult> DeleteDraftAsync(Guid submissionId,
        string ownerUserId, CancellationToken cancellationToken = default)
    {
        if (submissionId == Guid.Empty || !Guid.TryParseExact(ownerUserId, "D", out _))
            return HunterDraftDeleteResult.NotFound;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var row = await _db.HunterSubmissions
            .FromSqlInterpolated($"SELECT * FROM [Hunter_Submissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {submissionId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (row is null || row.UserId != ownerUserId) return HunterDraftDeleteResult.NotFound;
        if (row.Status != HunterSubmissionStatus.Draft) return HunterDraftDeleteResult.Conflict;
        if (await _db.HunterEvidence.AnyAsync(x => x.HunterSubmissionId == submissionId, cancellationToken))
            return HunterDraftDeleteResult.Conflict;
        // Capture sessions are retained by an FK/audit relationship. Only a
        // never-captured Draft can be physically deleted without destroying it.
        if (await _db.HunterCaptureSessions.AnyAsync(x => x.HunterSubmissionId == submissionId, cancellationToken))
            return HunterDraftDeleteResult.Conflict;
        var deleted = await _db.HunterSubmissions.Where(x => x.Id == submissionId && x.UserId == ownerUserId && x.Status == HunterSubmissionStatus.Draft)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted != 1) return HunterDraftDeleteResult.Conflict;
        await tx.CommitAsync(cancellationToken);
        return HunterDraftDeleteResult.Deleted;
    }
}
