
using System;
using System.Linq;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class CoinLedgerRollbackConcurrencyTests
{
    private static CoinLedgerDraftAggregate CreateWallet()
    {
        return new CoinLedgerDraftAggregate(
            Guid.NewGuid(),
            "user-001",
            "v1");
    }

    private static CoinRewardUniqueKey Hunter(string source)
    {
        return new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            source);
    }

    private static void FundWallet(
        CoinLedgerDraftAggregate wallet)
    {
        // 51 eligible Hunter rewards = 10,200 Coins.
        for (int i = 0; i < 51; i++)
        {
            Assert.True(wallet.TryCreditReward(
                Hunter($"hunt-{i}")));
        }
    }

    [Fact]
    public void WrongRecipient_DoesNotPartiallyChangeWallet()
    {
        var wallet = CreateWallet();
        var before = wallet.ReadBalance();

        var wrongKey = new CoinRewardUniqueKey(
            "another-user",
            CoinRewardType.HunterApproved,
            "hunt-001");

        Assert.Throws<InvalidOperationException>(() =>
            wallet.TryCreditReward(wrongKey));

        Assert.Equal(before, wallet.ReadBalance());
        Assert.Empty(wallet.ReadEntries());

        Assert.True(wallet.TryCreditReward(
            Hunter("hunt-001")));

        Assert.Equal(
            200,
            wallet.ReadBalance().PostedCoins);
    }

    [Fact]
    public void NullReward_DoesNotModifyLedger()
    {
        var wallet = CreateWallet();
        var before = wallet.ReadBalance();

        Assert.Throws<ArgumentNullException>(() =>
            wallet.TryCreditReward(null!));

        Assert.Equal(before, wallet.ReadBalance());
        Assert.Empty(wallet.ReadEntries());
    }

    [Fact]
    public void DuplicateNormalizedSource_CreditsOnlyOnce()
    {
        var wallet = CreateWallet();

        Assert.True(wallet.TryCreditReward(
            Hunter(" hunt-001 ")));

        Assert.False(wallet.TryCreditReward(
            Hunter("hunt-001")));

        Assert.Equal(
            200,
            wallet.ReadBalance().PostedCoins);

        Assert.Equal(
            200,
            wallet.ReadBalance().LifetimeEarnedCoins);

        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public void SameSourceDifferentRewardType_IsSeparateReward()
    {
        var wallet = CreateWallet();

        Assert.True(wallet.TryCreditReward(
            Hunter("source-001")));

        var gameCreator = new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.GameCreatorApproved,
            "source-001");

        Assert.True(wallet.TryCreditReward(gameCreator));

        Assert.Equal(
            300,
            wallet.ReadBalance().PostedCoins);

        Assert.Equal(2, wallet.ReadEntries().Count);
    }

    [Fact]
    public void ConcurrentDuplicateCredits_ProduceOneLedgerEntry()
    {
        var wallet = CreateWallet();
        var results = new bool[64];

        Parallel.For(0, results.Length, i =>
        {
            results[i] = wallet.TryCreditReward(
                Hunter("same-hunt"));
        });

        Assert.Equal(1, results.Count(x => x));

        var balance = wallet.ReadBalance();

        Assert.Equal(200, balance.PostedCoins);
        Assert.Equal(200, balance.LifetimeEarnedCoins);
        Assert.Equal(200, balance.SpendableCoins);

        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public void ConcurrentDistinctCredits_AreNotLost()
    {
        var wallet = CreateWallet();
        var results = new bool[64];

        Parallel.For(0, results.Length, i =>
        {
            results[i] = wallet.TryCreditReward(
                Hunter($"unique-hunt-{i}"));
        });

        Assert.Equal(64, results.Count(x => x));

        var balance = wallet.ReadBalance();
        var entries = wallet.ReadEntries();

        Assert.Equal(12800, balance.PostedCoins);
        Assert.Equal(12800, balance.LifetimeEarnedCoins);
        Assert.Equal(12800, balance.SpendableCoins);
        Assert.Equal(0, balance.ReservedCoins);

        Assert.Equal(64, entries.Count);
        Assert.Equal(
            64,
            entries.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void FailedReservation_LeavesWalletAndRequestRetryable()
    {
        var wallet = CreateWallet();
        var requestId = Guid.NewGuid();
        var before = wallet.ReadBalance();

        Assert.False(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.Equal(before, wallet.ReadBalance());
        Assert.Empty(wallet.ReadEntries());

        FundWallet(wallet);

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.Equal(
            10200,
            wallet.ReadBalance().PostedCoins);

        Assert.Equal(
            10000,
            wallet.ReadBalance().ReservedCoins);

        Assert.Equal(
            200,
            wallet.ReadBalance().SpendableCoins);

        Assert.DoesNotContain(
            wallet.ReadEntries(),
            x => x.DeltaCoins < 0);
    }

    [Fact]
    public void ConcurrentSameRequest_ReservesOnlyOnce()
    {
        var wallet = CreateWallet();
        FundWallet(wallet);

        var requestId = Guid.NewGuid();
        var results = new bool[32];

        Parallel.For(0, results.Length, i =>
        {
            results[i] = wallet.TryReserveSampleVoucher(
                requestId);
        });

        Assert.Equal(1, results.Count(x => x));

        Assert.Equal(
            10000,
            wallet.ReadBalance().ReservedCoins);

        Assert.Equal(
            200,
            wallet.ReadBalance().SpendableCoins);

        Assert.DoesNotContain(
            wallet.ReadEntries(),
            x => x.DeltaCoins < 0);
    }

    [Fact]
    public void ConcurrentDifferentRequests_CannotOverspend()
    {
        var wallet = CreateWallet();
        FundWallet(wallet);

        var results = new bool[32];

        Parallel.For(0, results.Length, i =>
        {
            results[i] = wallet.TryReserveSampleVoucher(
                Guid.NewGuid());
        });

        Assert.Equal(1, results.Count(x => x));

        Assert.Equal(
            10200,
            wallet.ReadBalance().PostedCoins);

        Assert.Equal(
            10000,
            wallet.ReadBalance().ReservedCoins);

        Assert.Equal(
            200,
            wallet.ReadBalance().SpendableCoins);
    }

    [Fact]
    public void FailedApproval_DoesNotConsumeRequestId()
    {
        var wallet = CreateWallet();
        FundWallet(wallet);

        var requestId = Guid.NewGuid();
        var before = wallet.ReadBalance();
        var entryCount = wallet.ReadEntries().Count;

        Assert.False(wallet.TryApproveSampleVoucher(
            requestId));

        Assert.Equal(before, wallet.ReadBalance());
        Assert.Equal(entryCount, wallet.ReadEntries().Count);

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.True(wallet.TryApproveSampleVoucher(
            requestId));

        Assert.Equal(
            200,
            wallet.ReadBalance().PostedCoins);

        Assert.Equal(
            0,
            wallet.ReadBalance().ReservedCoins);

        Assert.Single(
            wallet.ReadEntries(),
            x => x.DeltaCoins == -10000);
    }

    [Fact]
    public void FailedCancellation_DoesNotConsumeRequestId()
    {
        var wallet = CreateWallet();
        FundWallet(wallet);

        var requestId = Guid.NewGuid();
        var before = wallet.ReadBalance();

        Assert.False(wallet.TryCancelSampleVoucher(
            requestId));

        Assert.Equal(before, wallet.ReadBalance());

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.True(wallet.TryCancelSampleVoucher(
            requestId));

        Assert.Equal(
            10200,
            wallet.ReadBalance().PostedCoins);

        Assert.Equal(
            0,
            wallet.ReadBalance().ReservedCoins);

        Assert.DoesNotContain(
            wallet.ReadEntries(),
            x => x.DeltaCoins < 0);
    }

    [Fact]
    public void ConcurrentApproval_DebitsExactlyOnce()
    {
        var wallet = CreateWallet();
        FundWallet(wallet);

        var requestId = Guid.NewGuid();

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        var results = new bool[32];

        Parallel.For(0, results.Length, i =>
        {
            results[i] = wallet.TryApproveSampleVoucher(
                requestId);
        });

        Assert.Equal(1, results.Count(x => x));

        var balance = wallet.ReadBalance();

        Assert.Equal(200, balance.PostedCoins);
        Assert.Equal(0, balance.ReservedCoins);
        Assert.Equal(200, balance.SpendableCoins);
        Assert.Equal(10200, balance.LifetimeEarnedCoins);

        Assert.Single(
            wallet.ReadEntries(),
            x => x.DeltaCoins == -10000);
    }

    [Fact]
    public void ConcurrentApprovalAndCancellation_HaveOneWinner()
    {
        var wallet = CreateWallet();
        FundWallet(wallet);

        var requestId = Guid.NewGuid();

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        bool approved = false;
        bool cancelled = false;

        Parallel.Invoke(
            () => approved =
                wallet.TryApproveSampleVoucher(requestId),

            () => cancelled =
                wallet.TryCancelSampleVoucher(requestId));

        // Exactly one terminal action succeeds.
        Assert.True(approved ^ cancelled);

        var balance = wallet.ReadBalance();

        Assert.Equal(0, balance.ReservedCoins);

        if (approved)
        {
            Assert.Equal(200, balance.PostedCoins);

            Assert.Single(
                wallet.ReadEntries(),
                x => x.DeltaCoins == -10000);
        }
        else
        {
            Assert.Equal(10200, balance.PostedCoins);

            Assert.DoesNotContain(
                wallet.ReadEntries(),
                x => x.DeltaCoins < 0);
        }

        Assert.False(wallet.TryApproveSampleVoucher(
            requestId));

        Assert.False(wallet.TryCancelSampleVoucher(
            requestId));
    }

    [Fact]
    public void CancelledRequest_CannotLaterCreateDebit()
    {
        var wallet = CreateWallet();
        FundWallet(wallet);

        var requestId = Guid.NewGuid();

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.True(wallet.TryCancelSampleVoucher(
            requestId));

        var before = wallet.ReadBalance();

        Assert.False(wallet.TryApproveSampleVoucher(
            requestId));

        Assert.False(wallet.TryCancelSampleVoucher(
            requestId));

        Assert.False(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.Equal(before, wallet.ReadBalance());

        Assert.DoesNotContain(
            wallet.ReadEntries(),
            x => x.DeltaCoins < 0);
    }

    [Fact]
    public void LedgerRead_ReturnsStableSnapshot()
    {
        var wallet = CreateWallet();

        Assert.True(wallet.TryCreditReward(
            Hunter("hunt-first")));

        var snapshot = wallet.ReadEntries();

        Assert.Single(snapshot);

        Assert.True(wallet.TryCreditReward(
            Hunter("hunt-second")));

        Assert.Single(snapshot);

        Assert.Equal(2, wallet.ReadEntries().Count);

        Assert.Equal(
            400,
            wallet.ReadBalance().PostedCoins);
    }

    [Fact]
    public void EmptyRequestId_DoesNotModifyWallet()
    {
        var wallet = CreateWallet();
        var before = wallet.ReadBalance();

        Assert.Throws<ArgumentException>(() =>
            wallet.TryReserveSampleVoucher(Guid.Empty));

        Assert.Throws<ArgumentException>(() =>
            wallet.TryApproveSampleVoucher(Guid.Empty));

        Assert.Throws<ArgumentException>(() =>
            wallet.TryCancelSampleVoucher(Guid.Empty));

        Assert.Equal(before, wallet.ReadBalance());
        Assert.Empty(wallet.ReadEntries());
    }
}
