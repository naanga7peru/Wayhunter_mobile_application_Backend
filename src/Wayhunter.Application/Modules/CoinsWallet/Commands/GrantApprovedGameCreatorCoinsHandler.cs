
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

/// <summary>
/// M6-MS03-T03: Grant +100 Coins once for an
/// eligible Admin-approved, published game.
/// </summary>
public sealed class GrantApprovedGameCreatorCoinsHandler
{
    private readonly IGameCreatorApprovedRewardReader _reader;
    private readonly ICoinRewardGrantStore _rewardStore;
    private readonly string _ruleVersion;

    public GrantApprovedGameCreatorCoinsHandler(
        IGameCreatorApprovedRewardReader reader,
        ICoinRewardGrantStore rewardStore,
        string ruleVersion)
    {
        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));

        _rewardStore = rewardStore ??
            throw new ArgumentNullException(nameof(rewardStore));

        _ruleVersion =
            CoinLedgerVersionPolicy.RequireRuleVersion(
                ruleVersion);
    }

    public async Task<bool> HandleAsync(
        Guid gameId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));
        }

        // 1. Read trusted M5 approval data.
        var snapshot = await _reader.FindByGameIdAsync(
            gameId,
            cancellationToken);

        if (snapshot is null)
            return false;

        var game = snapshot.Game ??
            throw new InvalidOperationException(
                "Game record is required.");

        // 2. Prevent mismatched source references.
        if (game.Id != gameId)
        {
            throw new InvalidOperationException(
                "Game reference mismatch.");
        }

        // 3. Draft, Pending, Rejected, Expired and
        // Disabled games cannot create a new reward.
        if (game.Status != GameStatus.Published)
            return false;

        if (string.IsNullOrWhiteSpace(
                snapshot.ApprovalRecordId))
        {
            throw new InvalidOperationException(
                "Committed Admin approval is required.");
        }

        // 4. Reuse the existing M5 reward factory.
        // It validates approval and expiry timestamps,
        // recipient, reward amount and source ID.
        var reward =
            GameCreatorApprovalRewardFactory.Create(
                game,
                snapshot.ApprovalRecordId,
                Guid.NewGuid(),
                correlationId,
                _ruleVersion);

        // 5. The M6 reward store must atomically
        // enforce idempotency by recipient, type,
        // and stable GameId.
        return await _rewardStore.TryGrantOnceAsync(
            reward,
            cancellationToken);
    }
}
