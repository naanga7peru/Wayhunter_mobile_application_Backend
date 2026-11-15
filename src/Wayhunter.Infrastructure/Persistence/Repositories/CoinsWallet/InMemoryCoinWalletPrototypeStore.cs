
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

/// <summary>
/// DEVELOPMENT / TEST PROTOTYPE ONLY.
/// Stores balances in memory; all data is lost on restart.
/// Do not register as the production wallet repository.
/// </summary>
public sealed class InMemoryCoinWalletPrototypeStore :
    ICoinRewardGrantStore,
    ICoinWalletReader
{
    private readonly ConcurrentDictionary<
        string, CoinLedgerDraftAggregate> _wallets =
        new(StringComparer.Ordinal);

    public Task<bool> TryGrantOnceAsync(
        CoinRewardGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CoinRewardContractRules.ValidateGrant(request);

        string ownerId = request.RecipientUserId.Trim();
        string ruleVersion = request.RuleVersion.Trim();

        var wallet = _wallets.GetOrAdd(
            ownerId,
            id => new CoinLedgerDraftAggregate(
                Guid.NewGuid(),
                id,
                ruleVersion));

        if (!CoinLedgerVersionPolicy.MatchesRuleVersion(
            wallet.RuleVersion,
            ruleVersion))
        {
            throw new InvalidOperationException(
                "Wallet reward rule version mismatch.");
        }

        var rewardKey = new CoinRewardUniqueKey(
            ownerId,
            request.RewardType,
            request.SourceRecordId);

        // Aggregate lock protects concurrent writes
        // for this in-memory wallet instance.
        bool credited = wallet.TryCreditReward(rewardKey);

        return Task.FromResult(credited);
    }

    public Task<CoinWalletSnapshot?> GetByOwnerUserIdAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException(
                "Wallet owner is required.",
                nameof(ownerUserId));
        }

        string normalizedId = ownerUserId.Trim();

        if (!_wallets.TryGetValue(
            normalizedId,
            out var wallet))
        {
            return Task.FromResult<CoinWalletSnapshot?>(
                null);
        }

        var snapshot = new CoinWalletSnapshot(
            wallet.OwnerUserId,
            wallet.ReadBalance());

        return Task.FromResult<CoinWalletSnapshot?>(
            snapshot);
    }
}
