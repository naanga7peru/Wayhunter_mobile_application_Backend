using System;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterLeaderboardCandidateTests
{
    [Fact]
    public void Rank_UsesOnlyUniqueUnreversedHunterRewardsAndPreservesTiesAcrossPages()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        HunterLedgerCandidate[] ledger =
        [
            new(a, "a", CoinRewardType.HunterApproved, 200, false),
            new(a, "a", CoinRewardType.HunterApproved, 200, false),
            new(a, "voucher", null, -10000, null),
            new(b, "b", CoinRewardType.HunterApproved, 200, false),
            new(b, "old", CoinRewardType.HunterApproved, 200, true),
            new(c, "reversed", CoinRewardType.HunterApproved, 200, true)
        ];

        var page = Assert.Single(HunterLeaderboardCandidate.Rank(ledger, 1, 1));
        Assert.Equal(1, page.Rank);
        Assert.Equal(200, page.Coins);
        Assert.Empty(HunterLeaderboardCandidate.Rank(ledger, 2, 1));
    }

    [Fact]
    public void Rank_FailsClosedOnUnknownReversal()
    {
        HunterLedgerCandidate[] ledger = [new(Guid.NewGuid(), "hunt", CoinRewardType.HunterApproved, 200, null)];
        Assert.Throws<InvalidOperationException>(() => HunterLeaderboardCandidate.Rank(ledger, 0, 10));
    }
}
