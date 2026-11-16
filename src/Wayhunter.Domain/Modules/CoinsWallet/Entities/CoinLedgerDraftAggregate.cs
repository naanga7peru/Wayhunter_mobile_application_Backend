
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

namespace Wayhunter.Domain.Modules.CoinsWallet.Entities;

/// <summary>
/// M6 in-memory ledger prototype.
///
/// Voucher workflow:
/// Request -> Pending hold
/// Admin approval -> Approved, hold remains
/// Successful issuance -> One final debit
/// Cancellation of Pending -> Hold released
///
/// Not a production SQL transaction implementation.
/// </summary>
public sealed class CoinLedgerDraftAggregate
{
    private enum RequestState
    {
        Pending,
        Approved,
        Issued,
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
            if (_rewardKeys.Contains(key))
                return false;

            long amount =
                CoinRewardRules.GetAmount(key.RewardType);

            var current = CalculateBalance();

            long nextPosted =
                checked(current.PostedCoins + amount);

            long nextLifetime =
                checked(_lifetimeEarnedCoins + amount);

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

            _requests.Add(requestId, RequestState.Pending);

            // Reservation is not a ledger debit.
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

            // Approval only changes request state.
            // Reservation stays active.
            // No debit or ledger entry is created.
            _requests[requestId] = RequestState.Approved;

            return true;
        }
    }

    /// <summary>
    /// Simulates the result of a trusted voucher issuance.
    ///
    /// issuanceSucceeded must come from a trusted
    /// fulfillment operation, not directly from a user.
    ///
    /// This prototype is atomic only within one
    /// in-memory aggregate. Production SQL must
    /// commit issuance, debit and reservation consumption
    /// together in one database transaction.
    /// </summary>
    public bool TryIssueSampleVoucher(
        Guid requestId,
        bool issuanceSucceeded)
    {
        EnsureRequestId(requestId);

        lock (_gate)
        {
            if (!_requests.TryGetValue(
                requestId, out var state) ||
                state != RequestState.Approved)
            {
                return false;
            }

            if (!issuanceSucceeded)
            {
                // Failed issuance leaves the approved
                // request and its reservation intact.
                return false;
            }

            long cost =
                CoinLevelRedemptionRules.SampleVoucherCostCoins;

            var current = CalculateBalance();

            // Validate before modifying the ledger.
            _ = new CoinWalletBalance(
                checked(current.PostedCoins - cost),
                checked(current.ReservedCoins - cost),
                current.LifetimeEarnedCoins);

            var entry = CreateEntry(
                -cost,
                $"voucher-issued:{requestId:N}");

            _entries.Add(entry);

            // Reservation is consumed only now.
            _requests[requestId] = RequestState.Issued;

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

            _requests[requestId] = RequestState.Cancelled;

            // Pending hold released; no debit/refund.
            return true;
        }
    }

    private CoinWalletBalance CalculateBalance()
    {
        long posted = 0;
        long reserved = 0;

        foreach (var entry in _entries)
        {
            posted = checked(posted + entry.DeltaCoins);
        }

        foreach (var state in _requests.Values)
        {
            if (state is RequestState.Pending
                or RequestState.Approved)
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
