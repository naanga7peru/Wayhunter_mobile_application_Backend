using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;

namespace Wayhunter.Api.Controllers.Mobile.Games;

[ApiController]
[Authorize]
[Route("api/mobile/games/{gameId:guid}/attempts")]
public sealed class GameAttemptsController : ControllerBase
{
    private readonly TrustedIdentityResolutionService _identity;
    private readonly StartGameAttemptService _start;

    public GameAttemptsController(TrustedIdentityResolutionService identity, StartGameAttemptService start)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _start = start ?? throw new ArgumentNullException(nameof(start));
    }

    [HttpPost]
    [ProducesResponseType<GameAttemptResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<GameAttemptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartOrResume(Guid gameId, CancellationToken cancellationToken)
    {
        var account = await _identity.ResolveActiveAccountAsync(User, cancellationToken);
        if (account is null)
            return Error(StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "A valid active account is required.");

        try
        {
            var result = await _start.ExecuteAsync(gameId, account.UserId, cancellationToken);
            var response = new GameAttemptResponse(result.AttemptId, result.GameId, result.StartedAtUtc,
                result.DeadlineUtc, result.CurrentStepNumber, result.ResumedExistingAttempt);
            return result.ResumedExistingAttempt ? Ok(response) : StatusCode(StatusCodes.Status201Created, response);
        }
        catch (KeyNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "GAME_NOT_FOUND", "The requested Game was not found.");
        }
        catch (GameAttemptPersistenceUnavailableException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "GAME_ATTEMPTS_UNAVAILABLE", "Game attempts are temporarily unavailable.");
        }
        catch (InvalidOperationException)
        {
            return Error(StatusCodes.Status409Conflict, "GAME_ATTEMPT_CONFLICT", "The Game is not eligible or its attempt cannot be resumed.");
        }
    }

    private ObjectResult Error(int statusCode, string code, string message) =>
        new(ApiErrorResponse.Create(code, message, HttpContext.TraceIdentifier)) { StatusCode = statusCode };
}
