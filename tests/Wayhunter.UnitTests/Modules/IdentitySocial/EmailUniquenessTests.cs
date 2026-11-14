
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class EmailUniquenessTests
{
    [Fact]
    public void NormalizedEmail_ShouldHaveUniqueIndex()
    {
        // Inspect EF metadata without SQL connection.
        var modelBuilder = new ModelBuilder(new ConventionSet());

        modelBuilder.ApplyConfiguration(
            new UserAccountConfiguration());

        var userEntity = modelBuilder.Model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(userEntity);

        var emailIndex = Assert.Single(
            userEntity.GetIndexes());

        Assert.True(emailIndex.IsUnique);

        Assert.Equal(
            nameof(UserAccount.NormalizedEmail),
            Assert.Single(emailIndex.Properties).Name);

        Assert.Equal(
            "UX_Users_NormalizedEmail",
            emailIndex.FindAnnotation("Relational:Name")?.Value);
    }

    [Fact]
    public void EmailsWithDifferentCase_ShouldNormalizeEqually()
    {
        var registeredAt = new DateTimeOffset(
            2026, 11, 14, 0, 0, 0,
            TimeSpan.Zero);

        var first = UserAccount.RegisterPending(
            "Traveler One",
            "  USER@EXAMPLE.COM  ",
            "test-only-placeholder-hash",
            registeredAt);

        var second = UserAccount.RegisterPending(
            "Traveler Two",
            "user@example.com",
            "test-only-placeholder-hash",
            registeredAt);

        Assert.Equal(
            "user@example.com",
            first.NormalizedEmail);

        Assert.Equal(
            first.NormalizedEmail,
            second.NormalizedEmail);

        Assert.NotEqual(first.UserId, second.UserId);
    }
}
