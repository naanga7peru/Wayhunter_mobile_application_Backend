
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// M5-MS06-T05
/// Store contract for trusted Game proof decisions.
/// Implementations must verify reviewer authorization
/// before applying approval or rejection.
/// </summary>
public interface IGameStageProofDecisionStore
{
    Task<GameStageProofDecisionResultDto> ApplyDecisionAsync(
        GameStageProofDecisionCommand command,
        string authenticatedReviewerUserId,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken = default);
}
