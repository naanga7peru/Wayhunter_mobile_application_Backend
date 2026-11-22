using System.Security.Claims;
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

    public GameDraftApplicationAdapter(
        TrustedIdentityResolutionService identity,
        VerifiedGameDraftService verifiedDrafts,
        GameService games,
        GameDraftDeletionService deletion)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _verifiedDrafts = verifiedDrafts ?? throw new ArgumentNullException(nameof(verifiedDrafts));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _deletion = deletion ?? throw new ArgumentNullException(nameof(deletion));
    }

    public async Task<Game> CreateAsync(
        ClaimsPrincipal principal,
        CreateGameDraftDto request,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActiveActorAsync(principal, cancellationToken);
        return await _verifiedDrafts.CreateDraftAsync(
            actor,
            request,
            cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActiveActorAsync(principal, cancellationToken);
        await RequireOwnedDraftAsync(gameId, actor, cancellationToken);
        return await _verifiedDrafts.UpdateDraftAsync(
            gameId,
            actor,
            request,
            cancellationToken);
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

    private async Task RequireOwnedDraftAsync(
        Guid gameId,
        string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            await _games.GetGameDraftAsync(
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
