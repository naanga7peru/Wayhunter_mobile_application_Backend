using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Queries;

public sealed record HunterLedgerCandidate(
    Guid OwnerId, string SourceRecordId, CoinRewardType? RewardType,
    long DeltaCoins, bool? Reversed);

public sealed record HunterEarningsCandidate(Guid OwnerId, long Coins, int Rank);

/// <summary>Provisional RANK-01 ranking over already visibility-filtered, trusted ledger rows.</summary>
public static class HunterLeaderboardCandidate
{
    public static IReadOnlyList<HunterEarningsCandidate> Rank(
        IEnumerable<HunterLedgerCandidate> ledger, int offset, int limit)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var totals = ledger.Where(row => row.RewardType == CoinRewardType.HunterApproved && row.DeltaCoins > 0)
            .GroupBy(row => (row.OwnerId, row.SourceRecordId))
            .Select(group =>
            {
                var first = group.First();
                if (first.OwnerId == Guid.Empty || string.IsNullOrWhiteSpace(first.SourceRecordId) ||
                    first.Reversed is null || group.Any(row =>
                        row.DeltaCoins != first.DeltaCoins || row.Reversed != first.Reversed))
                {
                    throw new InvalidOperationException("Hunter reward provenance is incomplete or inconsistent.");
                }

                return first;
            })
            .Where(row => row.Reversed == false)
            .GroupBy(row => row.OwnerId)
            .Select(group => (Owner: group.Key, Coins: group.Sum(row => row.DeltaCoins)))
            .OrderByDescending(row => row.Coins)
            .ThenBy(row => row.Owner)
            .ToArray();

        var ranked = new List<HunterEarningsCandidate>();
        var rank = 0;
        for (var i = 0; i < totals.Length; i++)
        {
            if (i == 0 || totals[i].Coins != totals[i - 1].Coins)
                rank = i + 1;

            if (i >= offset && ranked.Count < limit)
                ranked.Add(new(totals[i].Owner, totals[i].Coins, rank));

            if (ranked.Count == limit)
                break;
        }

        return ranked;
    }
}
