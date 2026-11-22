
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class GameSubmissionService
{
    private readonly IGameSubmissionStore _submissionStore;

    public GameSubmissionService(
        IGameSubmissionStore submissionStore)
    {
        _submissionStore = submissionStore
            ?? throw new ArgumentNullException(
                nameof(submissionStore));
    }

    // =====================================
    // SUBMIT GAME FOR ADMIN REVIEW
    // =====================================

    public async Task<GameAdminQueueItemDto> SubmitAsync(
        Guid gameId,
        UserAccount creator,
        DateTimeOffset serverTimestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        // A persisted Active status alone is not sufficient evidence for
        // identity eligibility. Fail closed on malformed account snapshots
        // and deletion-requested accounts, matching M1's trusted identity
        // contract at this application boundary.
        if (!IsEligibleCreator(creator))
        {
            throw new InvalidOperationException(
                "Only active, verified users without a deletion request can submit games.");
        }

        // Check the server timestamp and game ID.
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (serverTimestampUtc == default ||
            serverTimestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid server UTC timestamp is required.",
                nameof(serverTimestampUtc));
        }

        // Reuse M1 Identity snapshot contract.
        // The display name is captured at submission.
        var identity = ApprovalIdentitySnapshotDto.Create(
            creator,
            gameId,
            serverTimestampUtc);

        // The store must enforce Game ownership,
        // Draft -> PendingApproval transition,
        // and atomic queue persistence.
        return await _submissionStore
            .SubmitDraftAndEnqueueAsync(
                identity,
                cancellationToken);
    }

    private static bool IsEligibleCreator(UserAccount creator)
    {
        if (creator.Status != UserAccountStatus.Active ||
            !creator.VerifiedAtUtc.HasValue ||
            creator.DeletionRequestedAtUtc.HasValue)
        {
            return false;
        }

        var createdAtUtc = creator.CreatedAtUtc;
        var verifiedAtUtc = creator.VerifiedAtUtc.Value;

        return createdAtUtc != default &&
            createdAtUtc.Offset == TimeSpan.Zero &&
            verifiedAtUtc != default &&
            verifiedAtUtc.Offset == TimeSpan.Zero &&
            verifiedAtUtc >= createdAtUtc;
    }
}
