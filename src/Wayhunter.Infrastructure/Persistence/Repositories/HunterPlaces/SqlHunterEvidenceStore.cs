using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

public sealed class SqlHunterEvidenceStore : IHunterEvidenceStore
{
    private readonly WayhunterSharedDbContext _db;
    public SqlHunterEvidenceStore(WayhunterSharedDbContext db) => _db = db;

    public async Task<HunterEvidenceBindResult> BindAsync(Guid submissionId, Guid sessionId,
        string ownerUserId, Guid hunterId, VerifiedHunterMediaReceipt receipt,
        DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default)
    {
        if (submissionId == Guid.Empty || sessionId == Guid.Empty || hunterId == Guid.Empty ||
            !Guid.TryParseExact(ownerUserId, "D", out var parsed) || parsed != hunterId ||
            receivedAtUtc == default || receivedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Evidence binding identity/time is invalid.");
        ArgumentNullException.ThrowIfNull(receipt);

        return await HunterSqlTransaction.ExecuteAsync(_db,
            () => BindCoreAsync(submissionId, sessionId, ownerUserId, hunterId, receipt, receivedAtUtc, cancellationToken), cancellationToken);
    }

    private async Task<HunterEvidenceBindResult> BindCoreAsync(Guid submissionId, Guid sessionId, string ownerUserId, Guid hunterId,
        VerifiedHunterMediaReceipt receipt, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var submission = await _db.HunterSubmissions
            .FromSqlInterpolated($"SELECT * FROM [Hunter_Submissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {submissionId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (submission is null || submission.UserId != ownerUserId || submission.Status != HunterSubmissionStatus.Draft)
            return new(false, false, Guid.Empty);

        var prior = await _db.HunterEvidence.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CaptureSessionId == sessionId, cancellationToken);
        if (prior is not null)
        {
            bool same = prior.HunterSubmissionId == submissionId && prior.OwnerUserId == ownerUserId &&
                prior.ProviderAssetIdHash == receipt.ProviderAssetId && prior.ProviderReceiptDigest == receipt.ReceiptDigest &&
                prior.EvidenceType == receipt.EvidenceType && prior.ProviderVerifiedAtUtc.HasValue;
            return same ? new(true, true, prior.Id) : new(false, false, Guid.Empty);
        }
        if (await _db.HunterEvidence.AnyAsync(x => x.ProviderAssetIdHash == receipt.ProviderAssetId,
                cancellationToken))
            return new(false, false, Guid.Empty);

        var session = await _db.HunterCaptureSessions
            .FromSqlInterpolated($"SELECT * FROM [Hunter_CaptureSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {sessionId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null || session.HunterId != hunterId || session.HunterSubmissionId != submissionId ||
            session.OwnerUserId != ownerUserId || session.IsConsumed || session.IsRevoked ||
            session.IssuedAtUtc > receipt.CapturedAtUtc || session.ExpiresAtUtc <= receivedAtUtc ||
            receipt.CapturedAtUtc > receivedAtUtc)
            return new(false, false, Guid.Empty);

        var existing = await _db.HunterEvidence.Where(x => x.HunterSubmissionId == submissionId && x.ProviderVerifiedAtUtc != null)
            .OrderBy(x => x.UploadedAtUtc).ToListAsync(cancellationToken);
        if (existing.Any(x => x.EvidenceType == receipt.EvidenceType)) return new(false, false, Guid.Empty);
        if (receipt.EvidenceType == HunterEvidenceType.LivePhoto)
        {
            var video = existing.SingleOrDefault(x => x.EvidenceType == HunterEvidenceType.Guided360Video);
            if (video is null || video.UploadedAtUtc >= receipt.CapturedAtUtc) return new(false, false, Guid.Empty);
        }

        var evidence = new HunterEvidence
        {
            Id = Guid.NewGuid(), HunterSubmissionId = submissionId, CaptureSessionId = sessionId,
            OwnerUserId = ownerUserId, EvidenceType = receipt.EvidenceType,
            PrivateMediaReference = receipt.PrivateMediaReference,
            ProviderAssetIdHash = receipt.ProviderAssetId, ProviderReceiptDigest = receipt.ReceiptDigest,
            ProviderVerifiedAtUtc = receivedAtUtc, CapturedAtUtc = receipt.CapturedAtUtc,
            UploadedAtUtc = receivedAtUtc, CaptureLatitude = receipt.CaptureLatitude,
            CaptureLongitude = receipt.CaptureLongitude, DurationSeconds = receipt.DurationSeconds
        };
        session.IsConsumed = true;
        await _db.HunterEvidence.AddAsync(evidence, cancellationToken);
        var consumed = await _db.HunterCaptureSessions.Where(x => x.Id == sessionId && x.HunterSubmissionId == submissionId &&
                x.HunterId == hunterId && !x.IsConsumed && !x.IsRevoked && x.ExpiresAtUtc > receivedAtUtc)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsConsumed, true), cancellationToken);
        if (consumed != 1) return new(false, false, Guid.Empty);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return new(false, false, Guid.Empty);
        }
        return new(true, false, evidence.Id);
    }

    public async Task<bool> HasCompleteVerifiedEvidenceAsync(Guid submissionId, string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.HunterEvidence.AsNoTracking()
            .Where(x => x.HunterSubmissionId == submissionId && x.OwnerUserId == ownerUserId &&
                        x.ProviderVerifiedAtUtc != null && x.CaptureSessionId != null &&
                        x.ProviderAssetIdHash != null && x.ProviderReceiptDigest != null)
            .OrderBy(x => x.UploadedAtUtc).ToListAsync(cancellationToken);
        if (rows.Count != 2 || rows[0].EvidenceType != HunterEvidenceType.Guided360Video ||
            rows[1].EvidenceType != HunterEvidenceType.LivePhoto) return false;
        if (!Guid.TryParseExact(ownerUserId, "D", out var hunterId)) return false;
        var sessionIds = rows.Select(x => x.CaptureSessionId!.Value).ToArray();
        var sessions = await _db.HunterCaptureSessions.AsNoTracking()
            .Where(x => sessionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        return rows[0].UploadedAtUtc < rows[1].CapturedAtUtc &&
            rows.All(x => x.CapturedAtUtc <= x.UploadedAtUtc && x.CapturedAtUtc.Offset == TimeSpan.Zero &&
                sessions.TryGetValue(x.CaptureSessionId!.Value, out var session) &&
                session.HunterId == hunterId && session.HunterSubmissionId == submissionId &&
                session.OwnerUserId == ownerUserId && session.IsConsumed && !session.IsRevoked &&
                x.CapturedAtUtc >= session.IssuedAtUtc && x.CapturedAtUtc < session.ExpiresAtUtc);
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is SqlException sql && sql.Number is 2601 or 2627) return true;
        return false;
    }
}
