
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Infrastructure.Persistence.Seeding.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class DevelopmentIdentitySeedFactoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldGenerateThreeDemoUsers()
    {
        var users = DevelopmentIdentitySeedFactory.Create(
            isDevelopment: true,
            createdAtUtc: Now);

        Assert.Equal(3, users.Count);
    }

    [Fact]
    public void Create_ShouldIncludeActiveUser()
    {
        var users = DevelopmentIdentitySeedFactory.Create(
            true, Now);

        var active = Assert.Single(
            users,
            user => user.Status == UserAccountStatus.Active);

        Assert.Equal(
            "active@wayhunter.invalid",
            active.NormalizedEmail);

        Assert.NotNull(active.VerifiedAtUtc);
    }

    [Fact]
    public void Create_ShouldIncludeUnverifiedUser()
    {
        var users = DevelopmentIdentitySeedFactory.Create(
            true, Now);

        var unverified = Assert.Single(
            users,
            user => user.Status ==
                UserAccountStatus.PendingVerification);

        Assert.Equal(
            "unverified@wayhunter.invalid",
            unverified.NormalizedEmail);

        Assert.Null(unverified.VerifiedAtUtc);
    }

    [Fact]
    public void Create_ShouldIncludeSuspendedUser()
    {
        var users = DevelopmentIdentitySeedFactory.Create(
            true, Now);

        var suspended = Assert.Single(
            users,
            user => user.Status == UserAccountStatus.Suspended);

        Assert.Equal(
            "suspended@wayhunter.invalid",
            suspended.NormalizedEmail);

        Assert.NotNull(suspended.VerifiedAtUtc);
    }

    [Fact]
    public void Create_ShouldRejectProductionEnvironment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentIdentitySeedFactory.Create(
                isDevelopment: false,
                createdAtUtc: Now));
    }

    [Fact]
    public void Create_ShouldUseUniqueIdsAndNoRealCredentials()
    {
        var users = DevelopmentIdentitySeedFactory.Create(
            true, Now);

        Assert.Equal(
            3,
            users.Select(user => user.UserId)
                .Distinct()
                .Count());

        Assert.All(users, user =>
        {
            Assert.True(Guid.TryParse(user.UserId, out _));

            Assert.EndsWith(
                "@wayhunter.invalid",
                user.NormalizedEmail);

            Assert.Equal(
                "!DEV_ONLY_INVALID_PASSWORD_HASH!",
                user.PasswordHash);

            Assert.Equal(Now, user.CreatedAtUtc);
        });
    }
}
