using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SqlTrustedIdentityAccountReaderTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static UserAccount CreateActiveAccount()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var account = UserAccount.RegisterPending(
            "Reader test",
            $"{Guid.NewGuid():N}@example.test",
            "test-hash-only",
            createdAt);
        account.ActivateAfterVerifiedOtp(createdAt.AddMinutes(1));
        return account;
    }

    private static ClaimsPrincipal Principal(string userId) => new(
        new ClaimsIdentity(
            [new Claim("sub", userId)],
            authenticationType: "unit-test"));

    [Fact]
    public async Task ExistingAccount_ReturnsMatchingAccountWithoutTracking()
    {
        await using var db = CreateContext();
        var account = CreateActiveAccount();
        db.Users.Add(account);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reader = new SqlTrustedIdentityAccountReader(db);
        var result = await reader.FindByUserIdAsync(account.UserId);

        Assert.NotNull(result);
        Assert.Equal(account.UserId, result.UserId);
        Assert.Equal(EntityState.Detached, db.Entry(result).State);
    }

    [Fact]
    public async Task ActiveAccount_IsAcceptedByTrustedResolver()
    {
        await using var db = CreateContext();
        var account = CreateActiveAccount();
        db.Users.Add(account);
        await db.SaveChangesAsync();

        var resolver = new TrustedIdentityResolutionService(
            new SqlTrustedIdentityAccountReader(db));

        var result = await resolver.ResolveActiveAccountAsync(
            Principal(account.UserId));

        Assert.NotNull(result);
        Assert.Equal(account.UserId, result.UserId);
    }

    [Fact]
    public async Task SuspendedAccount_IsRejectedByTrustedResolver()
    {
        await using var db = CreateContext();
        var account = CreateActiveAccount();
        account.Suspend();
        db.Users.Add(account);
        await db.SaveChangesAsync();

        var resolver = new TrustedIdentityResolutionService(
            new SqlTrustedIdentityAccountReader(db));

        var result = await resolver.ResolveActiveAccountAsync(
            Principal(account.UserId));

        Assert.Null(result);
    }

    [Fact]
    public async Task NonexistentId_ReturnsNull()
    {
        await using var db = CreateContext();
        var reader = new SqlTrustedIdentityAccountReader(db);

        Assert.Null(await reader.FindByUserIdAsync(
            Guid.NewGuid().ToString("D")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000000000000000000000000000")]
    public async Task MalformedOrEmptyId_ReturnsNull(string? userId)
    {
        await using var db = CreateContext();
        var reader = new SqlTrustedIdentityAccountReader(db);

        Assert.Null(await reader.FindByUserIdAsync(userId!));
    }

    [Fact]
    public async Task CancelledLookup_ThrowsCancellation()
    {
        await using var db = CreateContext();
        var reader = new SqlTrustedIdentityAccountReader(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.FindByUserIdAsync(
                Guid.NewGuid().ToString("D"),
                cts.Token));
    }

    [Fact]
    public async Task UnavailableContext_FailsClosed()
    {
        var db = CreateContext();
        var reader = new SqlTrustedIdentityAccountReader(db);
        db.Dispose();

        Assert.Null(await reader.FindByUserIdAsync(
            Guid.NewGuid().ToString("D")));
    }

    [Fact]
    public async Task ResolverUsesPrincipalIdentityNotCallerSuppliedLookupValue()
    {
        await using var db = CreateContext();
        var account = CreateActiveAccount();
        db.Users.Add(account);
        await db.SaveChangesAsync();

        var resolver = new TrustedIdentityResolutionService(
            new SqlTrustedIdentityAccountReader(db));

        Assert.Null(await resolver.ResolveActiveAccountAsync(
            Principal(Guid.NewGuid().ToString("D"))));
        Assert.NotNull(await resolver.ResolveActiveAccountAsync(
            Principal(account.UserId)));
    }
}
