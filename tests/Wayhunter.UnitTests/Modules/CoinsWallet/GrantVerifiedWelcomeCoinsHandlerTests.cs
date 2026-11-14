
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class GrantVerifiedWelcomeCoinsHandlerTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset VerifiedAt =
        CreatedAt.AddMinutes(5);

    private static UserAccount CreateAccount(
        bool verified = true)
    {
        var account = UserAccount.RegisterPending(
            "Wayhunter Test User",
            $"test-{Guid.NewGuid():N}@example.com",
            "unit-test-hash-not-a-real-password",
            CreatedAt);

        if (verified)
        {
            account.ActivateAfterVerifiedOtp(VerifiedAt);
        }

        return account;
    }

    private static GrantVerifiedWelcomeCoinsHandler CreateHandler(
        FakeAccountReader reader,
        FakeRewardStore store)
    {
        return new GrantVerifiedWelcomeCoinsHandler(
            reader,
            store,
            "2026-11-11");
    }

    [Fact]
    public async Task VerifiedUser_Receives100Coins()
    {
        var user = CreateAccount();
        var reader = new FakeAccountReader(user);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        bool granted = await handler.HandleAsync(
            user.UserId, Guid.NewGuid());

        Assert.True(granted);

        var wallet = store.GetWallet(user.UserId);
        Assert.NotNull(wallet);

        Assert.Equal(100, wallet.ReadBalance().PostedCoins);
        Assert.Equal(100, wallet.ReadBalance().SpendableCoins);
        Assert.Equal(100,
            wallet.ReadBalance().LifetimeEarnedCoins);

        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public async Task SameUserRetry_CreditsOnlyOnce()
    {
        var user = CreateAccount();
        var reader = new FakeAccountReader(user);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        Assert.True(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.False(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        var wallet = store.GetWallet(user.UserId);
        Assert.NotNull(wallet);

        Assert.Equal(100, wallet.ReadBalance().PostedCoins);
        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public async Task ConcurrentRetries_CreditExactlyOnce()
    {
        var user = CreateAccount();
        var reader = new FakeAccountReader(user);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
                handler.HandleAsync(
                    user.UserId,
                    Guid.NewGuid())));

        bool[] results = await Task.WhenAll(requests);

        Assert.Equal(1, results.Count(x => x));

        var wallet = store.GetWallet(user.UserId);
        Assert.NotNull(wallet);

        Assert.Equal(100, wallet.ReadBalance().PostedCoins);
        Assert.Equal(100,
            wallet.ReadBalance().LifetimeEarnedCoins);

        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public async Task DifferentVerifiedUsers_GetOwnRewards()
    {
        var first = CreateAccount();
        var second = CreateAccount();

        var reader = new FakeAccountReader(first, second);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        Assert.True(await handler.HandleAsync(
            first.UserId, Guid.NewGuid()));

        Assert.True(await handler.HandleAsync(
            second.UserId, Guid.NewGuid()));

        Assert.Equal(100,
            store.GetWallet(first.UserId)!
                .ReadBalance().PostedCoins);

        Assert.Equal(100,
            store.GetWallet(second.UserId)!
                .ReadBalance().PostedCoins);
    }

    [Fact]
    public async Task PendingOtpUser_GetsNoReward()
    {
        var user = CreateAccount(verified: false);
        var reader = new FakeAccountReader(user);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        bool granted = await handler.HandleAsync(
            user.UserId, Guid.NewGuid());

        Assert.False(granted);
        Assert.Null(store.GetWallet(user.UserId));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task SuspendedUser_GetsNoNewReward()
    {
        var user = CreateAccount();
        user.Suspend();

        var reader = new FakeAccountReader(user);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        Assert.False(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.Null(store.GetWallet(user.UserId));
    }

    [Fact]
    public async Task DeletionRequestedUser_GetsNoNewReward()
    {
        var user = CreateAccount();
        user.RequestDeletion(VerifiedAt.AddMinutes(1));

        var reader = new FakeAccountReader(user);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        Assert.False(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.Null(store.GetWallet(user.UserId));
    }

    [Fact]
    public async Task UnknownUser_GetsNoReward()
    {
        var reader = new FakeAccountReader();
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        Assert.False(await handler.HandleAsync(
            "missing-user", Guid.NewGuid()));

        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task EmptyUserId_IsRejected()
    {
        var handler = CreateHandler(
            new FakeAccountReader(),
            new FakeRewardStore());

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                "  ", Guid.NewGuid()));
    }

    [Fact]
    public async Task EmptyCorrelationId_IsRejected()
    {
        var user = CreateAccount();

        var handler = CreateHandler(
            new FakeAccountReader(user),
            new FakeRewardStore());

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                user.UserId, Guid.Empty));
    }

    [Fact]
    public async Task MismatchedAccount_IsRejected()
    {
        var user = CreateAccount();
        var otherUser = CreateAccount();

        var reader = new FakeAccountReader(user)
        {
            ForcedResult = otherUser
        };

        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                user.UserId, Guid.NewGuid()));

        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task WelcomeEvent_ContainsAuthoritativeDetails()
    {
        var user = CreateAccount();
        var store = new FakeRewardStore();
        var handler = CreateHandler(
            new FakeAccountReader(user), store);

        Guid correlationId = Guid.NewGuid();

        Assert.True(await handler.HandleAsync(
            user.UserId, correlationId));

        var reward = Assert.Single(store.ReceivedRequests);

        Assert.Equal(
            CoinRewardType.WelcomeVerified,
            reward.RewardType);

        Assert.Equal(user.UserId, reward.SourceRecordId);
        Assert.Equal(user.UserId, reward.RecipientUserId);
        Assert.Equal(100, reward.CoinsAmount);

        Assert.Equal(VerifiedAt,
            reward.EligibilityConfirmedAtUtc);

        Assert.Equal(correlationId, reward.CorrelationId);
        Assert.Equal("2026-11-11", reward.RuleVersion);

        Assert.NotEqual(Guid.Empty, reward.EventId);
        Assert.Null(reward.ApprovalRecordId);
    }

    [Fact]
    public async Task CancelledOperation_DoesNotGrant()
    {
        var user = CreateAccount();
        var reader = new FakeAccountReader(user);
        var store = new FakeRewardStore();
        var handler = CreateHandler(reader, store);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                user.UserId,
                Guid.NewGuid(),
                cancellation.Token));

        Assert.Equal(0, reader.Calls);
        Assert.Equal(0, store.Calls);
    }

    // Test-only M1 account reader.
    private sealed class FakeAccountReader
        : IVerifiedWelcomeAccountReader
    {
        private readonly ConcurrentDictionary<string, UserAccount>
            _accounts = new(StringComparer.Ordinal);

        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public UserAccount? ForcedResult { get; set; }

        public FakeAccountReader(params UserAccount[] accounts)
        {
            foreach (var account in accounts)
            {
                _accounts[account.UserId] = account;
            }
        }

        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _calls);

            if (ForcedResult is not null)
            {
                return Task.FromResult<UserAccount?>(
                    ForcedResult);
            }

            _accounts.TryGetValue(userId, out var account);

            return Task.FromResult<UserAccount?>(account);
        }
    }

    // Test-only ledger adapter.
    // Actual SQL-backed store will be implemented later.
    private sealed class FakeRewardStore : ICoinRewardGrantStore
    {
        private readonly ConcurrentDictionary<
            string, CoinLedgerDraftAggregate> _wallets =
            new(StringComparer.Ordinal);

        private readonly ConcurrentBag<CoinRewardGrantRequest>
            _requests = new();

        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public CoinRewardGrantRequest[] ReceivedRequests =>
            _requests.ToArray();

        public CoinLedgerDraftAggregate? GetWallet(
            string userId)
        {
            _wallets.TryGetValue(userId, out var wallet);
            return wallet;
        }

        public Task<bool> TryGrantOnceAsync(
            CoinRewardGrantRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _calls);

            CoinRewardContractRules.ValidateGrant(request);

            var wallet = _wallets.GetOrAdd(
                request.RecipientUserId,
                owner => new CoinLedgerDraftAggregate(
                    Guid.NewGuid(),
                    owner,
                    request.RuleVersion));

            var key = new CoinRewardUniqueKey(
                request.RecipientUserId,
                request.RewardType,
                request.SourceRecordId);

            bool granted = wallet.TryCreditReward(key);
            _requests.Add(request);

            return Task.FromResult(granted);
        }
    }
}
