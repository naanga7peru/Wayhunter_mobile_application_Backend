
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

namespace Wayhunter.Domain.Modules.CoinsWallet.Entities;

/// <summary>
/// In-memory ledger prototype for M6-MS02-T03.
/// All operations on one wallet are protected by a lock.
///
/// IMPORTANT:
/// This is NOT a production SQL transaction implementation.
/// Database persistence, unique indexes and EF Core migrations
/// require the approved shared database migration process.
/// </summary>
public sealed class CoinLedgerDraftAggregate
{
    private enum RequestState
    {
        Pending,
        Approved,
        Cancelled
    }

    private readonly object _gate = new();

    private readonly List<CoinLedgerEntrySchemaDraft>
        _entries = new();

    private readonly HashSet<CoinRewardUniqueKey>
        _rewardKeys = new();

    private readonly Dictionary<Guid, RequestState>
        _requests = new();

    private long _lifetimeEarnedCoins;

    public Guid WalletId { get; }
    public string OwnerUserId { get; }
    public string RuleVersion { get; }

    public CoinLedgerDraftAggregate(
        Guid walletId,
        string ownerUserId,
        string ruleVersion)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException(
                "Wallet ID required.", nameof(walletId));

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException(
                "Wallet owner required.", nameof(ownerUserId));

        WalletId = walletId;
        OwnerUserId = ownerUserId.Trim();

        RuleVersion =
            CoinLedgerVersionPolicy.RequireRuleVersion(
                ruleVersion);
    }

    public CoinWalletBalance ReadBalance()
    {
        lock (_gate)
        {
            return CalculateBalance();
        }
    }

    public IReadOnlyList<CoinLedgerEntrySchemaDraft>
        ReadEntries()
    {
        lock (_gate)
        {
            // Return a snapshot, not the mutable internal list.
            return _entries.ToArray();
        }
    }

    public bool TryCreditReward(CoinRewardUniqueKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!string.Equals(
            key.RecipientUserId,
            OwnerUserId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Reward recipient does not own this wallet.");
        }

        lock (_gate)
        {
            // Logical idempotency within this aggregate.
            if (_rewardKeys.Contains(key))
                return false;

            long amount =
                CoinRewardRules.GetAmount(key.RewardType);

            var current = CalculateBalance();

            long nextPosted =
                checked(current.PostedCoins + amount);

            long nextLifetime =
                checked(_lifetimeEarnedCoins + amount);

            // Validate projected state before modifying data.
            _ = new CoinWalletBalance(
                nextPosted,
                current.ReservedCoins,
                nextLifetime);

            var entry = CreateEntry(
                amount,
                $"reward:{key.RewardType}:{key.SourceRecordId}");

            _entries.Add(entry);
            _rewardKeys.Add(key);
            _lifetimeEarnedCoins = nextLifetime;

            return true;
        }
    }

    public bool TryReserveSampleVoucher(Guid requestId)
    {
        EnsureRequestId(requestId);

        lock (_gate)
        {
            // A processed request ID cannot be reused.
            if (_requests.ContainsKey(requestId))
                return false;

            var balance = CalculateBalance();

            if (!CoinLevelRedemptionRules
                .CanRequestSampleVoucher(balance))
            {
                return false;
            }

            long newReserved = checked(
                balance.ReservedCoins +
                CoinLevelRedemptionRules.SampleVoucherCostCoins);

            _ = new CoinWalletBalance(
                balance.PostedCoins,
                newReserved,
                balance.LifetimeEarnedCoins);

            // Reservation does not create a ledger debit.
            _requests.Add(requestId, RequestState.Pending);

            return true;
        }
    }

    public bool TryApproveSampleVoucher(Guid requestId)
    {
        EnsureRequestId(requestId);

        lock (_gate)
        {
            if (!_requests.TryGetValue(
                requestId, out var state) ||
                state != RequestState.Pending)
            {
                return false;
            }

            long cost =
                CoinLevelRedemptionRules.SampleVoucherCostCoins;

            var current = CalculateBalance();

            // Validate projected debit and reservation release.
            _ = new CoinWalletBalance(
                checked(current.PostedCoins - cost),
                checked(current.ReservedCoins - cost),
                current.LifetimeEarnedCoins);

            var entry = CreateEntry(
                -cost,
                $"voucher-approved:{requestId:N}");

            // Admin approval finalizes one debit.
            _entries.Add(entry);
            _requests[requestId] = RequestState.Approved;

            return true;
        }
    }

    public bool TryCancelSampleVoucher(Guid requestId)
    {
        EnsureRequestId(requestId);

        lock (_gate)
        {
            if (!_requests.TryGetValue(
                requestId, out var state) ||
                state != RequestState.Pending)
            {
                return false;
            }

            // Pending reservation is released.
            // No refund or compensating debit is created.
            _requests[requestId] = RequestState.Cancelled;

            return true;
        }
    }

    private CoinWalletBalance CalculateBalance()
    {
        long posted = 0;
        long reserved = 0;

        // Posted balance is derived from signed ledger entries.
        foreach (var entry in _entries)
        {
            posted = checked(posted + entry.DeltaCoins);
        }

        // Only pending reservations affect spendable Coins.
        foreach (var state in _requests.Values)
        {
            if (state == RequestState.Pending)
            {
                reserved = checked(
                    reserved +
                    CoinLevelRedemptionRules.SampleVoucherCostCoins);
            }
        }

        return new CoinWalletBalance(
            posted,
            reserved,
            _lifetimeEarnedCoins);
    }

    private CoinLedgerEntrySchemaDraft CreateEntry(
        long deltaCoins,
        string sourceRecordId)
    {
        return new CoinLedgerEntrySchemaDraft(
            Guid.NewGuid(),
            WalletId,
            deltaCoins,
            sourceRecordId,
            RuleVersion,
            DateTimeOffset.UtcNow);
    }

    private static void EnsureRequestId(Guid requestId)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException(
                "Voucher request ID required.",
                nameof(requestId));
    }
}
