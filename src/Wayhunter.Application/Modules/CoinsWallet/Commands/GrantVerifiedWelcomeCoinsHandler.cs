
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

/// <summary>
/// M6-MS03-T01: Grants the first welcome reward
/// after an M1 account has been verified and activated.
///
/// This handler is for trusted backend integration only.
/// It must not be exposed as a public grant endpoint.
/// </summary>
public sealed class GrantVerifiedWelcomeCoinsHandler
{
    private readonly IVerifiedWelcomeAccountReader _accounts;
    private readonly ICoinRewardGrantStore _rewardStore;
    private readonly string _ruleVersion;

    public GrantVerifiedWelcomeCoinsHandler(
        IVerifiedWelcomeAccountReader accounts,
        ICoinRewardGrantStore rewardStore,
        string ruleVersion)
    {
        _accounts = accounts ??
            throw new ArgumentNullException(nameof(accounts));

        _rewardStore = rewardStore ??
            throw new ArgumentNullException(nameof(rewardStore));

        _ruleVersion =
            CoinLedgerVersionPolicy.RequireRuleVersion(
                ruleVersion);
    }

    public async Task<bool> HandleAsync(
        string userId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));
        }

        string normalizedUserId = userId.Trim();

        // 1. Read the real account from trusted M1 storage.
        var account = await _accounts.FindByUserIdAsync(
            normalizedUserId,
            cancellationToken);

        if (account is null)
        {
            return false;
        }

        // Reject an incorrectly returned account.
        if (!string.Equals(
            account.UserId,
            normalizedUserId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Verified account ownership mismatch.");
        }

        // 2. Pending, suspended and deletion-requested
        // accounts cannot receive a new welcome credit.
        if (account.Status != UserAccountStatus.Active ||
            !account.VerifiedAtUtc.HasValue)
        {
            return false;
        }

        DateTimeOffset verifiedAtUtc =
            account.VerifiedAtUtc.Value;

        // 3. Trust only M1's recorded UTC verification.
        if (verifiedAtUtc == default ||
            verifiedAtUtc.Offset != TimeSpan.Zero ||
            verifiedAtUtc < account.CreatedAtUtc)
        {
            throw new InvalidOperationException(
                "Invalid verified account timestamp.");
        }

        // 4. Welcome Coins amount comes from M6 policy.
        const CoinRewardType rewardType =
            CoinRewardType.WelcomeVerified;

        int coinsAmount =
            CoinRewardRules.GetAmount(rewardType);

        // 5. The stable M1 UserId is the reward source.
        // EventId can change on retry; the durable ledger
        // must deduplicate using the stable source key.
        var reward = new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: rewardType,
            SourceRecordId: account.UserId,
            RecipientUserId: account.UserId,
            CoinsAmount: coinsAmount,
            EligibilityConfirmedAtUtc: verifiedAtUtc,
            CorrelationId: correlationId,
            RuleVersion: _ruleVersion
        );

        CoinRewardContractRules.ValidateGrant(reward);

        // 6. Only the ledger store may commit the credit.
        // The production store must enforce atomic
        // idempotency across requests and processes.
        return await _rewardStore.TryGrantOnceAsync(
            reward,
            cancellationToken);
    }
}
