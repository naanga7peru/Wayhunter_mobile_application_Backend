
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// M5-MS05-T02
/// Contract for submitting proof for
/// the player's current Game stage.
/// </summary>
public interface ISubmitGameStageProofStore
{
    Task<GameEvidence> SubmitCurrentStageProofAsync(
        SubmitGameStageProofCommand command,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
