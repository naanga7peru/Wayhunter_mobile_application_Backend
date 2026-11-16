
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// M5-MS06-T05
/// Trusted authorization boundary for Game proof decisions.
///
/// Reviewer identity must come from authenticated
/// server-side claims, never a mobile-app flag.
///
/// Production implementation must verify
/// the reviewer's permission for this Game.
/// </summary>
public interface IGameStageProofDecisionAuthorizer
{
    Task<bool> IsAuthorizedAsync(
        string authenticatedReviewerUserId,
        Guid gameId,
        CancellationToken cancellationToken = default);
}
