
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.CoinsWallet;

public sealed class M6SqlIntegrationFactAttribute : FactAttribute
{
    public M6SqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M6_TEST_SQL_CONNECTION")))
        {
            Skip = "M6 isolated SQL test database is not configured.";
        }
    }
}

public sealed class SqlCoinRewardConcurrencyTests
{
    private const string AllowedDatabase =
        "Wayhunter_Test_M5_eee96080bb0540ebaa5cd326f366d2fb";

    private const string AllowedServer =
        @"(localdb)\MSSQLLocalDB";

    private static string GetConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable(
            "WAYHUNTER_M6_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "M6 test SQL connection is missing.");

        var builder = new SqlConnectionStringBuilder(raw);

        if (!string.Equals(
                builder.InitialCatalog,
                AllowedDatabase,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                builder.DataSource,
                AllowedServer,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the dedicated M6 LocalDB test database is allowed.");
        }

        return builder.ConnectionString;
    }

    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(GetConnectionString())
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static async Task<string> CreateUserAsync()
    {
        await using var db = CreateContext();

        // Schema must already exist in this isolated test DB.
        // Do not generate or apply migrations here.
        var user = UserAccount.RegisterPending(
            "M6 SQL Test User",
            $"m6-test-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash",
            DateTimeOffset.UtcNow);

        user.ActivateAfterVerifiedOtp(DateTimeOffset.UtcNow);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.UserId;
    }

    private static CoinRewardGrantRequest CreateReward(
        string userId,
        CoinRewardType rewardType,
        string sourceId)
    {
        int amount = rewardType switch
        {
            CoinRewardType.WelcomeVerified => 100,
            CoinRewardType.HunterApproved => 200,
            CoinRewardType.GameCreatorApproved => 100,
            CoinRewardType.GameWinnerApproved => 100,
            _ => throw new ArgumentOutOfRangeException(
                nameof(rewardType))
        };

        return new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: rewardType,
            SourceRecordId: sourceId,
            RecipientUserId: userId,
            CoinsAmount: amount,
            EligibilityConfirmedAtUtc: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid(),
            RuleVersion: "m6-sql-test-v1",
            ApprovalRecordId: rewardType ==
                CoinRewardType.WelcomeVerified
                    ? null
                    : Guid.NewGuid().ToString("N"),
            GameAttemptId: rewardType ==
                CoinRewardType.GameWinnerApproved
                    ? Guid.NewGuid().ToString("N")
                    : null);
    }

    private static async Task<bool> GrantAsync(
        CoinRewardGrantRequest request)
    {
        await using var db = CreateContext();

        var store = new SqlCoinRewardGrantStore(db);

        return await store.TryGrantOnceAsync(request);
    }

    private static async Task CleanupAsync(string userId)
    {
        await using var db = CreateContext();

        var walletIds = await db.CoinWallets
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToArrayAsync();

        await db.CoinLedgerEntries
            .Where(x => walletIds.Contains(x.WalletId))
            .ExecuteDeleteAsync();

        await db.CoinWallets
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync();
    }

    [M6SqlIntegrationFact]
    public async Task SameRewardTwice_CreditsOnlyOnce()
    {
        string userId = await CreateUserAsync();

        try
        {
            var reward = CreateReward(
                userId,
                CoinRewardType.WelcomeVerified,
                Guid.NewGuid().ToString("N"));

            bool first = await GrantAsync(reward);
            bool second = await GrantAsync(reward);

            Assert.True(first);
            Assert.False(second);

            await using var db = CreateContext();

            var wallet = await db.CoinWallets
                .SingleAsync(x => x.UserId == userId);

            Assert.Equal(100, wallet.PostedCoins);
            Assert.Equal(100, wallet.LifetimeEarnedCoins);

            Assert.Equal(1, await db.CoinLedgerEntries
                .CountAsync(x => x.WalletId == wallet.Id));
        }
        finally
        {
            await CleanupAsync(userId);
        }
    }

    [M6SqlIntegrationFact]
    public async Task ConcurrentSameReward_CreditsOnlyOnce()
    {
        string userId = await CreateUserAsync();

        try
        {
            var reward = CreateReward(
                userId,
                CoinRewardType.HunterApproved,
                Guid.NewGuid().ToString("N"));

            var results = await Task.WhenAll(
                GrantAsync(reward),
                GrantAsync(reward));

            Assert.Single(results, x => x);
            Assert.Single(results, x => !x);

            await using var db = CreateContext();

            var wallet = await db.CoinWallets
                .SingleAsync(x => x.UserId == userId);

            Assert.Equal(200, wallet.PostedCoins);
            Assert.Equal(200, wallet.LifetimeEarnedCoins);

            Assert.Equal(1, await db.CoinLedgerEntries
                .CountAsync(x => x.WalletId == wallet.Id));
        }
        finally
        {
            await CleanupAsync(userId);
        }
    }

    [M6SqlIntegrationFact]
    public async Task ConcurrentDifferentRewards_BothAreRecorded()
    {
        string userId = await CreateUserAsync();

        try
        {
            var welcome = CreateReward(
                userId,
                CoinRewardType.WelcomeVerified,
                Guid.NewGuid().ToString("N"));

            var hunter = CreateReward(
                userId,
                CoinRewardType.HunterApproved,
                Guid.NewGuid().ToString("N"));

            var results = await Task.WhenAll(
                GrantAsync(welcome),
                GrantAsync(hunter));

            Assert.All(results, Assert.True);

            await using var db = CreateContext();

            var wallet = await db.CoinWallets
                .SingleAsync(x => x.UserId == userId);

            Assert.Equal(300, wallet.PostedCoins);
            Assert.Equal(300, wallet.LifetimeEarnedCoins);
            Assert.Equal(0, wallet.ReservedCoins);

            Assert.Equal(2, await db.CoinLedgerEntries
                .CountAsync(x => x.WalletId == wallet.Id));
        }
        finally
        {
            await CleanupAsync(userId);
        }
    }
}
