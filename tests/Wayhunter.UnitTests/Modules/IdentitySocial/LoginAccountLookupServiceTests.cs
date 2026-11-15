
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class LoginAccountLookupServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateAccount(
        string email = "traveler@example.com")
    {
        return UserAccount.RegisterPending(
            "Wayhunter Traveler",
            email,
            "test-only-password-hash",
            Now);
    }

    private sealed class FakeReader : ILoginAccountReader
    {
        public UserAccount? Account { get; set; }

        public string? LastEmail { get; private set; }

        public int Calls { get; private set; }

        public Task<UserAccount?> FindByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Calls++;
            LastEmail = normalizedEmail;

            return Task.FromResult(Account);
        }
    }

    [Fact]
    public async Task ValidEmail_FindsAccount()
    {
        var account = CreateAccount();
        var reader = new FakeReader { Account = account };
        var service = new LoginAccountLookupService(reader);

        var result = await service.FindByEmailAsync(
            "traveler@example.com");

        Assert.Same(account, result);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task UppercaseEmail_IsNormalized()
    {
        var account = CreateAccount();
        var reader = new FakeReader { Account = account };
        var service = new LoginAccountLookupService(reader);

        var result = await service.FindByEmailAsync(
            " TRAVELER@EXAMPLE.COM ");

        Assert.Same(account, result);
        Assert.Equal(
            "traveler@example.com",
            reader.LastEmail);
    }

    [Fact]
    public async Task InvalidEmail_DoesNotQueryStorage()
    {
        var reader = new FakeReader();
        var service = new LoginAccountLookupService(reader);

        var result = await service.FindByEmailAsync(
            "invalid-email");

        Assert.Null(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task EmptyEmail_DoesNotQueryStorage()
    {
        var reader = new FakeReader();
        var service = new LoginAccountLookupService(reader);

        var result = await service.FindByEmailAsync("  ");

        Assert.Null(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task MissingAccount_ReturnsNull()
    {
        var reader = new FakeReader();
        var service = new LoginAccountLookupService(reader);

        var result = await service.FindByEmailAsync(
            "missing@example.com");

        Assert.Null(result);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task WrongAccount_IsRejected()
    {
        var reader = new FakeReader
        {
            Account = CreateAccount("other@example.com")
        };

        var service = new LoginAccountLookupService(reader);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FindByEmailAsync(
                "traveler@example.com"));
    }

    [Fact]
    public async Task CancelledRequest_DoesNotQueryStorage()
    {
        var reader = new FakeReader();
        var service = new LoginAccountLookupService(reader);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.FindByEmailAsync(
                "traveler@example.com",
                cancellation.Token));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public void NullReader_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoginAccountLookupService(null!));
    }
}
