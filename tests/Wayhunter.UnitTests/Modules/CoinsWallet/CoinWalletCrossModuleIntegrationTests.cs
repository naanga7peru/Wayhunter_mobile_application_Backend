
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Queries;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class CoinWalletCrossModuleIntegrationTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateUser(
        bool verified = true)
    {
        var user = UserAccount.RegisterPending(
            "Wayhunter Test",
            $"test-{Guid.NewGuid():N}@example.com",
            "test-hash",
            CreatedAt);

        if (verified)
        {
            user.ActivateAfterVerifiedOtp(
                CreatedAt.AddMinutes(5));
        }

        return user;
    }

    private static ClaimsPrincipal Authenticated(
        string userId)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId)
                },
                "UnitTest"));
    }

    private static HunterApprovedRewardSnapshot
        HunterApproval(
            string ownerId,
            HunterSubmissionStatus status =
                HunterSubmissionStatus.Approved)
    {
        Guid id = Guid.NewGuid();

        return new HunterApprovedRewardSnapshot(
            SubmissionId: id,
            HunterUserId: ownerId,
            SubmissionStatus: status,
            SubmittedAtUtc: CreatedAt,
            ReviewId: Guid.NewGuid(),
            ReviewSubmissionId: id,
            ReviewStatus: status,
            ReviewerAdminUserId: "admin-001",
            ReviewedAtUtc: CreatedAt.AddMinutes(10));
    }

    [Fact]
    public async Task VerifiedWelcome_UpdatesWalletRead()
    {
        var user = CreateUser();
        var store = new InMemoryCoinWalletPrototypeStore();

        var welcome = new GrantVerifiedWelcomeCoinsHandler(
            new FakeAccountReader(user),
            store,
            "v1");

        Assert.True(await welcome.HandleAsync(
            user.UserId,
            Guid.NewGuid()));

        var walletService = new GetMyCoinWalletService(store);

        var wallet = await walletService.GetMineAsync(
            Authenticated(user.UserId));

        Assert.NotNull(wallet);
        Assert.Equal(100, wallet.PostedCoins);
        Assert.Equal(100, wallet.SpendableCoins);
        Assert.Equal(100, wallet.LifetimeEarnedCoins);
    }

    [Fact]
    public async Task WelcomeAndHunter_Total300Coins()
    {
        var user = CreateUser();
        var store = new InMemoryCoinWalletPrototypeStore();

        var welcome = new GrantVerifiedWelcomeCoinsHandler(
            new FakeAccountReader(user),
            store,
            "v1");

        var approval = HunterApproval(user.UserId);

        var hunter = new GrantApprovedHunterCoinsHandler(
            new FakeHunterReader(approval),
            store,
            "v1");

        Assert.True(await welcome.HandleAsync(
            user.UserId,
            Guid.NewGuid()));

        Assert.True(await hunter.HandleAsync(
            approval.SubmissionId,
            Guid.NewGuid()));

        var wallet = await new GetMyCoinWalletService(store)
            .GetMineAsync(Authenticated(user.UserId));

        Assert.NotNull(wallet);
        Assert.Equal(300, wallet.PostedCoins);
        Assert.Equal(300, wallet.SpendableCoins);
        Assert.Equal(300, wallet.LifetimeEarnedCoins);
        Assert.Equal(0, wallet.ReservedCoins);
    }

    [Fact]
    public async Task RetriedWelcomeAndHunter_DoNotDoubleCredit()
    {
        var user = CreateUser();
        var store = new InMemoryCoinWalletPrototypeStore();

        var welcome = new GrantVerifiedWelcomeCoinsHandler(
            new FakeAccountReader(user),
            store,
            "v1");

        var approval = HunterApproval(user.UserId);

        var hunter = new GrantApprovedHunterCoinsHandler(
            new FakeHunterReader(approval),
            store,
            "v1");

        Assert.True(await welcome.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.False(await welcome.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.True(await hunter.HandleAsync(
            approval.SubmissionId, Guid.NewGuid()));

        Assert.False(await hunter.HandleAsync(
            approval.SubmissionId, Guid.NewGuid()));

        var wallet = await new GetMyCoinWalletService(store)
            .GetMineAsync(Authenticated(user.UserId));

        Assert.NotNull(wallet);
        Assert.Equal(300, wallet.PostedCoins);
        Assert.Equal(300, wallet.LifetimeEarnedCoins);
    }

    [Fact]
    public async Task UnverifiedAccount_ReceivesNothing()
    {
        var user = CreateUser(verified: false);
        var store = new InMemoryCoinWalletPrototypeStore();

        var welcome = new GrantVerifiedWelcomeCoinsHandler(
            new FakeAccountReader(user),
            store,
            "v1");

        Assert.False(await welcome.HandleAsync(
            user.UserId,
            Guid.NewGuid()));

        var wallet = await store.GetByOwnerUserIdAsync(
            user.UserId);

        Assert.Null(wallet);
    }

    [Fact]
    public async Task RejectedHunter_ReceivesNothing()
    {
        var store = new InMemoryCoinWalletPrototypeStore();

        var approval = HunterApproval(
            "hunter-001",
            HunterSubmissionStatus.Rejected);

        var hunter = new GrantApprovedHunterCoinsHandler(
            new FakeHunterReader(approval),
            store,
            "v1");

        Assert.False(await hunter.HandleAsync(
            approval.SubmissionId,
            Guid.NewGuid()));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            approval.HunterUserId));
    }

    [Fact]
    public async Task ConcurrentSameHunter_CreditsOnlyOnce()
    {
        var store = new InMemoryCoinWalletPrototypeStore();

        var approval = HunterApproval("hunter-001");

        var hunter = new GrantApprovedHunterCoinsHandler(
            new FakeHunterReader(approval),
            store,
            "v1");

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
                hunter.HandleAsync(
                    approval.SubmissionId,
                    Guid.NewGuid())));

        bool[] results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(x => x));

        var wallet = await new GetMyCoinWalletService(store)
            .GetMineAsync(
                Authenticated(approval.HunterUserId));

        Assert.NotNull(wallet);
        Assert.Equal(200, wallet.PostedCoins);
        Assert.Equal(200, wallet.LifetimeEarnedCoins);
    }

    [Fact]
    public async Task WalletRead_CannotExposeAnotherUser()
    {
        var user = CreateUser();
        var store = new InMemoryCoinWalletPrototypeStore();

        var welcome = new GrantVerifiedWelcomeCoinsHandler(
            new FakeAccountReader(user),
            store,
            "v1");

        Assert.True(await welcome.HandleAsync(
            user.UserId,
            Guid.NewGuid()));

        var service = new GetMyCoinWalletService(store);

        var otherUserWallet = await service.GetMineAsync(
            Authenticated("different-user"));

        Assert.Null(otherUserWallet);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetMineAsync(
                new ClaimsPrincipal()));

        var ownerWallet = await service.GetMineAsync(
            Authenticated(user.UserId));

        Assert.NotNull(ownerWallet);
        Assert.Equal(100, ownerWallet.PostedCoins);
    }

    private sealed class FakeAccountReader
        : IVerifiedWelcomeAccountReader
    {
        private readonly UserAccount _account;

        public FakeAccountReader(UserAccount account)
        {
            _account = account;
        }

        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UserAccount? found =
                userId == _account.UserId ? _account : null;

            return Task.FromResult(found);
        }
    }

    private sealed class FakeHunterReader
        : IHunterApprovedRewardReader
    {
        private readonly HunterApprovedRewardSnapshot _approval;

        public FakeHunterReader(
            HunterApprovedRewardSnapshot approval)
        {
            _approval = approval;
        }

        public Task<HunterApprovedRewardSnapshot?>
            FindBySubmissionIdAsync(
                Guid submissionId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HunterApprovedRewardSnapshot? found =
                submissionId == _approval.SubmissionId
                    ? _approval
                    : null;

            return Task.FromResult(found);
        }
    }
}
