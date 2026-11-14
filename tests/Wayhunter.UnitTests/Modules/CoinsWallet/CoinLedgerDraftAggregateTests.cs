
using System;
using System.Linq;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinLedgerDraftAggregateTests
{
    private static CoinLedgerDraftAggregate CreateWallet()
    {
        return new CoinLedgerDraftAggregate(
            Guid.NewGuid(),
            "user-001",
            "v1");
    }

    private static CoinRewardUniqueKey CreateReward(
        string source)
    {
        return new CoinRewardUniqueKey(
            "user-001",
            CoinRewardType.HunterApproved,
            source);
    }

    private static CoinLedgerDraftAggregate FundWallet()
    {
        var wallet = CreateWallet();

        // 51 distinct approved Hunter sources = 10,200 Coins.
        for (int i = 0; i < 51; i++)
        {
            Assert.True(wallet.TryCreditReward(
                CreateReward($"hunt-{i}")));
        }

        return wallet;
    }

    [Fact]
    public void NewWallet_HasZeroBalance()
    {
        var wallet = CreateWallet();
        var balance = wallet.ReadBalance();

        Assert.Equal(0, balance.PostedCoins);
        Assert.Equal(0, balance.ReservedCoins);
        Assert.Equal(0, balance.SpendableCoins);
        Assert.Empty(wallet.ReadEntries());
    }

    [Fact]
    public void RewardCredit_UpdatesLedgerAndBalance()
    {
        var wallet = CreateWallet();

        Assert.True(wallet.TryCreditReward(
            CreateReward("hunt-1")));

        var balance = wallet.ReadBalance();

        Assert.Equal(200, balance.PostedCoins);
        Assert.Equal(200, balance.SpendableCoins);
        Assert.Equal(200, balance.LifetimeEarnedCoins);
        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public void DuplicateReward_DoesNotDoubleCredit()
    {
        var wallet = CreateWallet();

        Assert.True(wallet.TryCreditReward(
            CreateReward("hunt-1")));

        Assert.False(wallet.TryCreditReward(
            CreateReward("hunt-1")));

        Assert.Equal(200,
            wallet.ReadBalance().PostedCoins);

        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public void ConcurrentDuplicateReward_CreditsOnce()
    {
        var wallet = CreateWallet();
        var results = new bool[20];

        Parallel.For(0, 20, i =>
        {
            results[i] = wallet.TryCreditReward(
                CreateReward("hunt-1"));
        });

        Assert.Equal(1, results.Count(x => x));

        Assert.Equal(200,
            wallet.ReadBalance().PostedCoins);

        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public void InsufficientBalance_CannotReserveVoucher()
    {
        var wallet = CreateWallet();

        Assert.False(wallet.TryReserveSampleVoucher(
            Guid.NewGuid()));

        Assert.Equal(0,
            wallet.ReadBalance().ReservedCoins);
    }

    [Fact]
    public void Reservation_ReducesSpendableWithoutDebit()
    {
        var wallet = FundWallet();
        var requestId = Guid.NewGuid();

        Assert.True(
            wallet.TryReserveSampleVoucher(requestId));

        Assert.False(
            wallet.TryReserveSampleVoucher(requestId));

        var balance = wallet.ReadBalance();

        Assert.Equal(10200, balance.PostedCoins);
        Assert.Equal(10000, balance.ReservedCoins);
        Assert.Equal(200, balance.SpendableCoins);

        // Reservation must not create a permanent debit.
        Assert.DoesNotContain(
            wallet.ReadEntries(),
            entry => entry.DeltaCoins < 0);
    }

    [Fact]
    public void ConcurrentReservations_DoNotOverspend()
    {
        var wallet = FundWallet();
        var results = new bool[20];

        Parallel.For(0, 20, i =>
        {
            results[i] = wallet.TryReserveSampleVoucher(
                Guid.NewGuid());
        });

        Assert.Equal(1, results.Count(x => x));

        Assert.Equal(10000,
            wallet.ReadBalance().ReservedCoins);

        Assert.Equal(200,
            wallet.ReadBalance().SpendableCoins);
    }

    [Fact]
    public void AdminApproval_PostsDebitAndConsumesHold()
    {
        var wallet = FundWallet();
        var requestId = Guid.NewGuid();

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.True(wallet.TryApproveSampleVoucher(
            requestId));

        var balance = wallet.ReadBalance();

        Assert.Equal(200, balance.PostedCoins);
        Assert.Equal(0, balance.ReservedCoins);
        Assert.Equal(200, balance.SpendableCoins);

        // Spending must not reduce lifetime earned.
        Assert.Equal(
            10200,
            balance.LifetimeEarnedCoins);

        Assert.Single(
       wallet.ReadEntries(),
       x => x.DeltaCoins == -10000);
    }

    [Fact]
    public void RepeatedApproval_DoesNotDoubleDebit()
    {
        var wallet = FundWallet();
        var requestId = Guid.NewGuid();

        wallet.TryReserveSampleVoucher(requestId);

        Assert.True(wallet.TryApproveSampleVoucher(
            requestId));

        Assert.False(wallet.TryApproveSampleVoucher(
            requestId));

        Assert.Equal(200,
            wallet.ReadBalance().PostedCoins);

        Assert.Single(
     wallet.ReadEntries(),
     x => x.DeltaCoins < 0);
    }

    [Fact]
    public void Cancellation_ReleasesReservation()
    {
        var wallet = FundWallet();
        var requestId = Guid.NewGuid();

        Assert.True(wallet.TryReserveSampleVoucher(
            requestId));

        Assert.True(wallet.TryCancelSampleVoucher(
            requestId));

        var balance = wallet.ReadBalance();

        Assert.Equal(10200, balance.PostedCoins);
        Assert.Equal(0, balance.ReservedCoins);
        Assert.Equal(10200, balance.SpendableCoins);

        Assert.False(wallet.TryCancelSampleVoucher(
            requestId));
    }

    [Fact]
    public void CancelledRequest_CannotBeApproved()
    {
        var wallet = FundWallet();
        var requestId = Guid.NewGuid();

        wallet.TryReserveSampleVoucher(requestId);
        wallet.TryCancelSampleVoucher(requestId);

        Assert.False(wallet.TryApproveSampleVoucher(
            requestId));

        Assert.DoesNotContain(
            wallet.ReadEntries(),
            entry => entry.DeltaCoins < 0);
    }

    [Fact]
    public void WrongRecipient_RewardIsRejected()
    {
        var wallet = CreateWallet();

        var reward = new CoinRewardUniqueKey(
            "another-user",
            CoinRewardType.HunterApproved,
            "hunt-1");

        Assert.Throws<InvalidOperationException>(() =>
            wallet.TryCreditReward(reward));

        Assert.Empty(wallet.ReadEntries());
    }
}
