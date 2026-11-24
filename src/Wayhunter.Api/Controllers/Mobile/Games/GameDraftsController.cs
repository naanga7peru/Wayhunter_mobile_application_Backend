using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Api.Controllers.Mobile.Games;

[ApiController]
[ServiceFilter(typeof(GameDraftAuthenticationFilter))]
[Route("api/mobile/games/drafts")]
public sealed class GameDraftsController : ControllerBase
{
    private readonly GameDraftApplicationAdapter _drafts;
    private readonly ILogger<GameDraftsController> _logger;

    public GameDraftsController(
        GameDraftApplicationAdapter drafts,
        ILogger<GameDraftsController> logger)
    {
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [ProducesResponseType<GameDraftResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateGameDraftDto request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var game = await _drafts.CreateAsync(
                User, request, idempotencyKey, cancellationToken);
            var response = GameDraftResponseDto.From(game);
            SetRevision(response.Revision);

            return CreatedAtAction(
                nameof(Read),
                new { gameId = game.Id },
                response);
        });
    }

    [HttpGet("{gameId:guid}")]
    [ProducesResponseType<GameDraftResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Read(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var response = GameDraftResponseDto.From(await _drafts.ReadAsync(
                User, gameId, cancellationToken));
            SetRevision(response.Revision);
            return Ok(response);
        });
    }

    [HttpPut("{gameId:guid}")]
    [ProducesResponseType<GameDraftResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid gameId,
        [FromBody] CreateGameDraftDto request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var expectedRevision = Request.Headers.IfMatch.ToString().Trim('"');
            var response = GameDraftResponseDto.From(await _drafts.UpdateAsync(
                User, gameId, request, expectedRevision, cancellationToken));
            SetRevision(response.Revision);
            return Ok(response);
        });
    }

    [HttpPost("{gameId:guid}/submit")]
    [ProducesResponseType<GameAdminQueueItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid gameId, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Ok(await _drafts.SubmitAsync(User, gameId, cancellationToken)));

    [HttpDelete("{gameId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            await _drafts.DeleteAsync(User, gameId, cancellationToken);
            return NoContent();
        });
    }

    private async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (GameDraftIdentityRejectedException)
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "AUTHENTICATION_REQUIRED",
                "A valid active account is required.");
        }
        catch (KeyNotFoundException)
        {
            return Error(
                StatusCodes.Status404NotFound,
                "GAME_DRAFT_NOT_FOUND",
                "The requested draft was not found.");
        }
        catch (ArgumentException)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "GAME_DRAFT_INVALID",
                "The draft request is invalid.");
        }
        catch (UnauthorizedAccessException)
        {
            // Existing media verification rejects every unverified media ID.
            return Error(
                StatusCodes.Status400BadRequest,
                "GAME_DRAFT_MEDIA_INVALID",
                "One or more draft media references are invalid.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error(
                StatusCodes.Status409Conflict,
                "GAME_DRAFT_REVISION_CONFLICT",
                "The draft changed during this operation.");
        }
        catch (GameDraftRevisionConflictException)
        {
            return Error(
                StatusCodes.Status409Conflict,
                "GAME_DRAFT_REVISION_CONFLICT",
                "The draft changed during this operation.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Game draft operation was rejected.");
            return Error(
                StatusCodes.Status409Conflict,
                "GAME_DRAFT_STATE_CONFLICT",
                "The draft is not in an editable state.");
        }
    }

    private ObjectResult Error(
        int statusCode,
        string code,
        string message) => new(ApiErrorResponse.Create(
        code,
        message,
        HttpContext.TraceIdentifier))
    {
        StatusCode = statusCode
    };

    private void SetRevision(string? revision)
    {
        if (!string.IsNullOrWhiteSpace(revision))
            Response.Headers.ETag = $"\"{revision}\"";
    }
}

public sealed record GameDraftResponseDto(
    Guid GameId,
    string? Title,
    string? Description,
    int? DurationMinutes,
    string Status,
    IReadOnlyList<GameDraftStageResponseDto> Stages,
    string? Revision)
{
    public static GameDraftResponseDto From(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var hints = game.DraftHints.ToDictionary(
            hint => hint.GameStepId);

        var stages = game.DraftSteps
            .OrderBy(step => step.StepNumber)
            .Select(step =>
            {
                hints.TryGetValue(step.Id, out var hint);
                return new GameDraftStageResponseDto(
                    step.StepNumber,
                    hint?.TextHint,
                    hint?.PhotoMediaId);
            })
            .ToArray();

        return new GameDraftResponseDto(
            game.Id,
            game.Title,
            game.Description,
            game.DurationMinutes,
            game.Status.ToString(),
            stages,
            game.PersistenceRevision is null
                ? null
                : Convert.ToBase64String(game.PersistenceRevision));
    }
}

public sealed record GameDraftStageResponseDto(
    int StepNumber,
    string? TextHint,
    string? PhotoMediaId);

public sealed class GameDraftAuthenticationFilter
    : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identities.Any(identity =>
                identity.IsAuthenticated &&
                string.Equals(
                    identity.AuthenticationType,
                    SessionBearerAuthenticationHandler.SchemeName,
                    StringComparison.Ordinal)))
            return Task.CompletedTask;

        context.Result = new ObjectResult(ApiErrorResponse.Create(
            "AUTHENTICATION_REQUIRED",
            "A valid authenticated session is required.",
            context.HttpContext.TraceIdentifier))
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };

        return Task.CompletedTask;
    }
}
