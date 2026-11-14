
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Validators;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class GameClueMediaOwnershipService
{
    private readonly IGameClueMediaAccessVerifier _verifier;

    public GameClueMediaOwnershipService(
        IGameClueMediaAccessVerifier verifier)
    {
        _verifier = verifier
            ?? throw new ArgumentNullException(
                nameof(verifier));
    }

    // =====================================
    // VERIFY CREATOR'S CLUE MEDIA
    // =====================================

    public async Task ValidateCreatorClueMediaAsync(
        string creatorUserId,
        CreateGameDraftDto draft,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(creatorUserId))
        {
            throw new ArgumentException(
                "Creator User ID is required.",
                nameof(creatorUserId));
        }

        // Validate title, duration, stage order
        // and required hints first.
        GameDraftValidator.Validate(draft);

        // Avoid checking the same media ID repeatedly.
        var checkedMediaIds = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var stage in draft.Stages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Text-only clues do not need media checks.
            if (string.IsNullOrWhiteSpace(stage.PhotoMediaId))
                continue;

            var mediaId = stage.PhotoMediaId.Trim();

            if (!checkedMediaIds.Add(mediaId))
                continue;

            // The verifier must confirm:
            // - Media exists and is ready
            // - Media belongs to this creator
            // - Media is allowed as a game clue
            // - Media is NOT private player proof
            var allowed = await _verifier
                .IsUsableCreatorClueMediaAsync(
                    mediaId,
                    creatorUserId,
                    cancellationToken);

            if (!allowed)
            {
                throw new UnauthorizedAccessException(
                    "Game clue media is not authorized.");
            }
        }
    }
}
