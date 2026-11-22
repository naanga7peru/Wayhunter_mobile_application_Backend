using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;

namespace Wayhunter.Api.Controllers.Mobile.Games;

[ApiController]
[Authorize]
[Route("api/mobile/games")]
public sealed class GamesReadController : ControllerBase
{
    private readonly TrustedIdentityResolutionService _identity;
    private readonly GetAvailableGamesQueryService _availableGames;
    private readonly GetPublicGameDetailsQueryService _gameDetails;
    private readonly GetMyGameAttemptsQueryService _myAttempts;

    public GamesReadController(
        TrustedIdentityResolutionService identity,
        GetAvailableGamesQueryService availableGames,
        GetPublicGameDetailsQueryService gameDetails,
        GetMyGameAttemptsQueryService myAttempts)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _availableGames = availableGames ?? throw new ArgumentNullException(nameof(availableGames));
        _gameDetails = gameDetails ?? throw new ArgumentNullException(nameof(gameDetails));
        _myAttempts = myAttempts ?? throw new ArgumentNullException(nameof(myAttempts));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PublicGameSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!await HasActiveAccountAsync(cancellationToken))
            return Error(StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "A valid active account is required.");

        try
        {
            return Ok(await _availableGames.ExecuteAsync(cancellationToken));
        }
        catch (GamesReadDependencyUnavailableException)
        {
            return Unavailable();
        }
    }

    [HttpGet("{gameId:guid}")]
    [ProducesResponseType(typeof(PublicGameDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Read(Guid gameId, CancellationToken cancellationToken)
    {
        if (!await HasActiveAccountAsync(cancellationToken))
            return Error(StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "A valid active account is required.");

        try
        {
            var game = await _gameDetails.ExecuteAsync(gameId, cancellationToken);
            return game is null
                ? Error(StatusCodes.Status404NotFound, "GAME_NOT_FOUND", "The requested Game was not found.")
                : Ok(game);
        }
        catch (GamesReadDependencyUnavailableException)
        {
            return Unavailable();
        }
    }

    [HttpGet("me/attempts")]
    [ProducesResponseType(typeof(IReadOnlyList<MyGameAttemptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> MyAttempts(CancellationToken cancellationToken)
    {
        var account = await _identity.ResolveActiveAccountAsync(User, cancellationToken);
        if (account is null)
            return Error(StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "A valid active account is required.");

        try
        {
            return Ok(await _myAttempts.ExecuteAsync(account.UserId, cancellationToken));
        }
        catch (GamesReadDependencyUnavailableException)
        {
            return Unavailable();
        }
    }

    private async Task<bool> HasActiveAccountAsync(CancellationToken cancellationToken) =>
        await _identity.ResolveActiveAccountAsync(User, cancellationToken) is not null;

    private ObjectResult Unavailable() => Error(
        StatusCodes.Status503ServiceUnavailable,
        "GAMES_READ_UNAVAILABLE",
        "Games data is temporarily unavailable.");

    private ObjectResult Error(int statusCode, string code, string message) =>
        new(ApiErrorResponse.Create(code, message, HttpContext.TraceIdentifier))
        {
            StatusCode = statusCode
        };
}
