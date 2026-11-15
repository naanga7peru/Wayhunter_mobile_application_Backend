
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

/// <summary>
/// Reads authoritative M5 game and Admin approval data.
/// Production implementation must use committed backend records.
/// </summary>
public sealed record GameCreatorApprovedRewardSnapshot(
    Game Game,
    string ApprovalRecordId);

public interface IGameCreatorApprovedRewardReader
{
    Task<GameCreatorApprovedRewardSnapshot?> FindByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);
}
