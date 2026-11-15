
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class TrustedHunterCaptureSessionIdentityResolverTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeAccountReader
        : ITrustedIdentityAccountReader
    {
        public UserAccount? Account { get; set; }

        public int ReadCount { get; private set; }

        public string? LastUserId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Action? OnRead { get; set; }

        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            LastUserId = userId;
            LastToken = cancellationToken;

            OnRead?.Invoke();

            return Task.FromResult<UserAccount?>(Account);
        }
    }

    private static UserAccount CreateAccount(
        bool active = true)
    {
        var account = UserAccount.RegisterPending(
            "Hunter Test User",
            $"hunter-{Guid.NewGuid():N}@example.com",
            "test-only-password-hash",
            Now);

        if (active)
        {
            account.ActivateAfterVerifiedOtp(
                Now.AddMinutes(5));
        }

        return account;
    }

    private static (
        TrustedHunterCaptureSessionIdentityResolver Resolver,
        FakeAccountReader Reader)
        CreateResolver(UserAccount? account = null)
    {
        var reader = new FakeAccountReader
        {
            Account = account
        };

        var resolver =
            new TrustedHunterCaptureSessionIdentityResolver(
                reader);

        return (resolver, reader);
    }

    [Fact]
    public void NullReader_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TrustedHunterCaptureSessionIdentityResolver(
                null!));
    }

    [Fact]
    public async Task ActiveVerifiedAccount_ShouldResolveHunterId()
    {
        var account = CreateAccount();

        var (resolver, reader) =
            CreateResolver(account);

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                account.UserId);

        Assert.NotNull(result);
        Assert.Equal(
            Guid.Parse(account.UserId),
            result.Value);

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(account.UserId, reader.LastUserId);
    }

    [Fact]
    public async Task UppercaseGuid_ShouldUseCanonicalUserId()
    {
        var account = CreateAccount();

        var (resolver, reader) =
            CreateResolver(account);

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                account.UserId.ToUpperInvariant());

        Assert.Equal(
            Guid.Parse(account.UserId),
            result);

        Assert.Equal(account.UserId, reader.LastUserId);
        Assert.Equal(1, reader.ReadCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    public async Task InvalidUserId_ShouldReturnNull(
        string? userId)
    {
        var (resolver, reader) = CreateResolver();

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                userId!);

        Assert.Null(result);
        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task EmptyGuid_ShouldReturnNull()
    {
        var (resolver, reader) = CreateResolver();

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                Guid.Empty.ToString("D"));

        Assert.Null(result);
        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task MissingAccount_ShouldReturnNull()
    {
        var (resolver, reader) = CreateResolver();

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                Guid.NewGuid().ToString("D"));

        Assert.Null(result);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task PendingAccount_ShouldBeRejected()
    {
        var account = CreateAccount(active: false);

        var (resolver, reader) =
            CreateResolver(account);

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                account.UserId);

        Assert.Null(result);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task SuspendedAccount_ShouldBeRejected()
    {
        var account = CreateAccount();
        account.Suspend();

        var (resolver, reader) =
            CreateResolver(account);

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                account.UserId);

        Assert.Null(result);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task DeletionRequestedAccount_ShouldBeRejected()
    {
        var account = CreateAccount();

        account.RequestDeletion(
            Now.AddMinutes(10));

        var (resolver, reader) =
            CreateResolver(account);

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                account.UserId);

        Assert.Null(result);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task WrongAccountOwner_ShouldBeRejected()
    {
        var account = CreateAccount();

        var (resolver, reader) =
            CreateResolver(account);

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                Guid.NewGuid().ToString("D"));

        Assert.Null(result);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotReadAccount()
    {
        var account = CreateAccount();

        var (resolver, reader) =
            CreateResolver(account);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveSessionHunterIdAsync(
                account.UserId,
                cts.Token));

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task CancellationDuringLookup_ShouldStopResolution()
    {
        var account = CreateAccount();

        var (resolver, reader) =
            CreateResolver(account);

        using var cts = new CancellationTokenSource();

        reader.OnRead = cts.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveSessionHunterIdAsync(
                account.UserId,
                cts.Token));

        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task CancellationToken_ShouldBeForwarded()
    {
        var account = CreateAccount();

        var (resolver, reader) =
            CreateResolver(account);

        using var cts = new CancellationTokenSource();

        var result =
            await resolver.ResolveSessionHunterIdAsync(
                account.UserId,
                cts.Token);

        Assert.NotNull(result);
        Assert.Equal(cts.Token, reader.LastToken);
        Assert.Equal(1, reader.ReadCount);
    }
}
