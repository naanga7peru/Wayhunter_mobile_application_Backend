using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Api.Controllers.Mobile.Games;

/// <summary>
/// Application adapter for the M5 draft HTTP API. MVC discovery of the
/// controller that consumes it is separately gated to explicit Development
/// configuration; the adapter itself contains no route metadata.
/// </summary>
public sealed class GameDraftApplicationAdapter
{
    private readonly TrustedIdentityResolutionService _identity;
    private readonly VerifiedGameDraftService _verifiedDrafts;
    private readonly GameService _games;
    private readonly GameDraftDeletionService _deletion;
    private readonly GameSubmissionService? _submission;
    private readonly TimeProvider? _clock;

    public GameDraftApplicationAdapter(
        TrustedIdentityResolutionService identity,
        VerifiedGameDraftService verifiedDrafts,
        GameService games,
        GameDraftDeletionService deletion,
        GameSubmissionService? submission = null,
        TimeProvider? clock = null)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _verifiedDrafts = verifiedDrafts ?? throw new ArgumentNullException(nameof(verifiedDrafts));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _deletion = deletion ?? throw new ArgumentNullException(nameof(deletion));
        _submission = submission;
        _clock = clock;
    }

    public async Task<GameAdminQueueItemDto> SubmitAsync(
        ClaimsPrincipal principal, Guid gameId, CancellationToken cancellationToken = default)
    {
        var account = await _identity.ResolveActiveAccountAsync(principal, cancellationToken)
            ?? throw new GameDraftIdentityRejectedException("An authenticated active account is required.");
        if (_submission is null || _clock is null)
            throw new InvalidOperationException("Game submission is not configured.");
        return await _submission.SubmitAsync(gameId, account,
            _clock.GetUtcNow().ToUniversalTime(), cancellationToken);
    }

    public async Task<Game> CreateAsync(
        ClaimsPrincipal principal,
        CreateGameDraftDto request,
        string idempotencyKey = "unit-test",
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActiveActorAsync(principal, cancellationToken);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128 || idempotencyKey.Any(char.IsControl))
            throw new ArgumentException("A valid Idempotency-Key of at most 128 characters is required.");
        var canonical = new
        {
            Title = request.Title?.Trim(),
            Description = request.Description?.Trim(),
            request.DurationMinutes,
            Stages = request.Stages?.OrderBy(x => x.StepNumber).Select(x => new
            {
                x.StepNumber,
                TextHint = x.TextHint?.Trim(),
                PhotoMediaId = x.PhotoMediaId?.Trim()
            }).ToArray()
        };
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical));
        return await _verifiedDrafts.CreateDraftIdempotentlyAsync(
            actor, request, idempotencyKey.Trim(), hash, cancellationToken);
    }

    public async Task<Game> ReadAsync(
        ClaimsPrincipal principal,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActiveActorAsync(principal, cancellationToken);
        try
        {
            return await _games.GetGameDraftAsync(
                gameId,
                actor,
                cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new KeyNotFoundException(
                "Game draft not found.", ex);
        }
    }

    public async Task<Game> UpdateAsync(
        ClaimsPrincipal principal,
        Guid gameId,
        CreateGameDraftDto request,
        string? expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActiveActorAsync(principal, cancellationToken);
        var current = await RequireOwnedDraftAsync(gameId, actor, cancellationToken);
        byte[]? revision = null;
        if (current.PersistenceRevision is not null)
        {
            if (string.IsNullOrWhiteSpace(expectedRevision))
                throw new ArgumentException("Draft revision is required.");
            try
            {
                revision = Convert.FromBase64String(expectedRevision);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Draft revision is invalid.");
            }

            if (!current.PersistenceRevision.AsSpan().SequenceEqual(revision))
                throw new GameDraftRevisionConflictException();
        }

        return await _verifiedDrafts.UpdateDraftAsync(
            gameId,
            actor,
            request,
            cancellationToken,
            revision);
    }

    public async Task DeleteAsync(
        ClaimsPrincipal principal,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActiveActorAsync(principal, cancellationToken);
        try
        {
            await _deletion.DeleteGameDraftAsync(
                gameId,
                actor,
                cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new KeyNotFoundException(
                "Game draft not found.", ex);
        }
    }

    private async Task<Game> RequireOwnedDraftAsync(
        Guid gameId,
        string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _games.GetGameDraftAsync(
                gameId, actor, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new KeyNotFoundException(
                "Game draft not found.", ex);
        }
    }

    private async Task<string> RequireActiveActorAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var account = await _identity.ResolveActiveAccountAsync(
            principal,
            cancellationToken);

        if (account is null)
        {
            // The future HTTP layer must map this using the approved
            // platform contract; this adapter defines no public status code.
            throw new GameDraftIdentityRejectedException(
                "An authenticated active account is required.");
        }

        return account.UserId;
    }
}

internal sealed class GameDraftIdentityRejectedException(string message)
    : UnauthorizedAccessException(message);
