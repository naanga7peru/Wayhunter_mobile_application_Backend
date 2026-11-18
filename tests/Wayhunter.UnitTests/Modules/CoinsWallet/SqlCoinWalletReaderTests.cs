
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class SqlCoinWalletReaderTests
{
    private static WayhunterSharedDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    [Fact]
    public async Task ExistingOwner_ReturnsCorrectBalance()
    {
        await using var db = CreateDb();

        db.CoinWallets.Add(new CoinWalletRow
        {
            Id = Guid.NewGuid(),
            UserId = "user-001",
            PostedCoins = 10100,
            ReservedCoins = 10000,
            LifetimeEarnedCoins = 12000
        });

        await db.SaveChangesAsync();

        var reader = new SqlCoinWalletReader(db);

        var result = await reader.GetByOwnerUserIdAsync(
            "user-001");

        Assert.NotNull(result);
        Assert.Equal("user-001", result.OwnerUserId);
        Assert.Equal(10100, result.Balance.PostedCoins);
        Assert.Equal(10000, result.Balance.ReservedCoins);
        Assert.Equal(100, result.Balance.SpendableCoins);
        Assert.Equal(12000, result.Balance.LifetimeEarnedCoins);
    }

    [Fact]
    public async Task MissingWallet_ReturnsNull()
    {
        await using var db = CreateDb();

        var reader = new SqlCoinWalletReader(db);

        var result = await reader.GetByOwnerUserIdAsync(
            "missing-user");

        Assert.Null(result);
    }

    [Fact]
    public async Task Reader_ReturnsOnlyRequestedOwnerWallet()
    {
        await using var db = CreateDb();

        db.CoinWallets.AddRange(
            new CoinWalletRow
            {
                Id = Guid.NewGuid(),
                UserId = "user-001",
                PostedCoins = 100,
                ReservedCoins = 0,
                LifetimeEarnedCoins = 100
            },
            new CoinWalletRow
            {
                Id = Guid.NewGuid(),
                UserId = "user-002",
                PostedCoins = 500,
                ReservedCoins = 100,
                LifetimeEarnedCoins = 500
            });

        await db.SaveChangesAsync();

        var reader = new SqlCoinWalletReader(db);

        var result = await reader.GetByOwnerUserIdAsync(
            "user-002");

        Assert.NotNull(result);
        Assert.Equal("user-002", result.OwnerUserId);
        Assert.Equal(500, result.Balance.PostedCoins);
        Assert.Equal(400, result.Balance.SpendableCoins);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankOwnerId_IsRejected(string ownerId)
    {
        await using var db = CreateDb();

        var reader = new SqlCoinWalletReader(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => reader.GetByOwnerUserIdAsync(ownerId));
    }

    [Fact]
    public async Task OwnerId_IsTrimmedBeforeLookup()
    {
        await using var db = CreateDb();

        db.CoinWallets.Add(new CoinWalletRow
        {
            Id = Guid.NewGuid(),
            UserId = "user-001",
            PostedCoins = 200,
            ReservedCoins = 0,
            LifetimeEarnedCoins = 200
        });

        await db.SaveChangesAsync();

        var reader = new SqlCoinWalletReader(db);

        var result = await reader.GetByOwnerUserIdAsync(
            "  user-001  ");

        Assert.NotNull(result);
        Assert.Equal("user-001", result.OwnerUserId);
    }
}
