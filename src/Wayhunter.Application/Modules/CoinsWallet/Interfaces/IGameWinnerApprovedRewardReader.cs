
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

/// <summary>
/// M6-MS03-T04
/// Trusted M5 Game Winner approval contract.
/// </summary>
public enum GameWinnerReviewOutcome
{
    Pending = 1,
    Rejected = 2,
    Approved = 3
}

/// <summary>
/// Authoritative Game winner approval snapshot.
/// Production implementation must read committed
/// Admin review records, not client-supplied flags.
/// </summary>
public sealed record GameWinnerApprovedRewardSnapshot(
    Game Game,
    GameAttempt Attempt,
    GameWinnerReviewOutcome ReviewOutcome,
    string ApprovalRecordId,
    string ReviewerAdminUserId,
    DateTimeOffset VerifiedAtUtc
);

public interface IGameWinnerApprovedRewardReader
{
    Task<GameWinnerApprovedRewardSnapshot?>
        FindByGameIdAsync(
            Guid gameId,
            CancellationToken cancellationToken = default);
}
