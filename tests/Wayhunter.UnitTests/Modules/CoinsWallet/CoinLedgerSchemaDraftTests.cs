
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinLedgerSchemaDraftTests
{
    [Fact]
    public void Wallet_WithValidBalance_IsCreated()
    {
        var wallet = new CoinWalletSchemaDraft(
            Guid.NewGuid(), "user-001",
            10100, 10000, 12000);

        Assert.Equal(10100, wallet.PostedCoins);
        Assert.Equal(10000, wallet.ReservedCoins);
        Assert.Equal(12000, wallet.LifetimeEarnedCoins);
    }

    [Fact]
    public void Wallet_WithExcessiveReserve_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoinWalletSchemaDraft(
                Guid.NewGuid(), "user-001",
                100, 200, 100));
    }

    [Fact]
    public void Wallet_WithoutUser_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoinWalletSchemaDraft(
                Guid.NewGuid(), "",
                100, 0, 100));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(-10000)]
    public void Ledger_AllowsNonZeroCoinDelta(long delta)
    {
        var entry = new CoinLedgerEntrySchemaDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            delta,
            "source-001",
            "v1",
            DateTimeOffset.UtcNow);

        Assert.Equal(delta, entry.DeltaCoins);
    }

    [Fact]
    public void Ledger_RejectsZeroDelta()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoinLedgerEntrySchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
                "source-001",
                "v1",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Ledger_RequiresSourceRecordId()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoinLedgerEntrySchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                100,
                "",
                "v1",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Reservation_RequiresPositiveCoins()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoinReservationSchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                0));
    }

    [Fact]
    public void Reservation_AcceptsApprovedVoucherCost()
    {
        var reservation = new CoinReservationSchemaDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            10000);

        Assert.Equal(10000, reservation.ReservedCoins);
    }

    [Fact]
    public void RuleVersion_StoresDecimalLkrReference()
    {
        var rule = new CoinRuleVersionSchemaDraft(
            Guid.NewGuid(),
            "v1",
            10000,
            100m);

        Assert.Equal(10000, rule.VoucherCostCoins);
        Assert.Equal(100m, rule.VoucherReferenceLkr);
    }

    [Fact]
    public void RuleVersion_RejectsNegativeLkr()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoinRuleVersionSchemaDraft(
                Guid.NewGuid(),
                "v1",
                10000,
                -100m));
    }
}
