using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SqlTrustedAdministratorRoleReaderTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static UserRoleAssignmentRow CreateActive(
        string userId)
    {
        return UserRoleAssignmentRow.CreateTrusted(
            userId,
            DateTimeOffset.UtcNow,
            null);
    }

    [Fact]
    public async Task MissingAssignment_ReturnsNull()
    {
        using var db = CreateContext();
        var reader = new SqlTrustedAdministratorRoleReader(db);

        var result = await reader.FindAssignmentAsync(
            Guid.NewGuid().ToString("D"));

        Assert.Null(result);
    }

    [Fact]
    public async Task InvalidUserId_ReturnsNull()
    {
        using var db = CreateContext();
        var reader = new SqlTrustedAdministratorRoleReader(db);

        Assert.Null(await reader.FindAssignmentAsync("invalid"));
    }

    [Fact]
    public async Task SingleActiveAdminAssignment_ReturnsAdmin()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        db.UserRoleAssignments.Add(CreateActive(userId));
        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        var result = await reader.FindAssignmentAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("Admin", result.Role);
        Assert.Equal(
            AdministratorRoleAssignmentStatus.Active,
            result.Status);
    }

    [Fact]
    public async Task RevokedAssignment_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        var assignment = CreateActive(userId);
        assignment.RevokeTrusted(
            DateTimeOffset.UtcNow.AddMinutes(1),
            null);

        db.UserRoleAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        Assert.Null(await reader.FindAssignmentAsync(userId));
    }

    [Fact]
    public async Task HistoricalRevokedAndOneActive_ReturnsAdmin()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        var historical = CreateActive(userId);
        historical.RevokeTrusted(
            DateTimeOffset.UtcNow.AddMinutes(1),
            null);

        db.UserRoleAssignments.Add(historical);
        db.UserRoleAssignments.Add(CreateActive(userId));
        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        var result = await reader.FindAssignmentAsync(userId);

        Assert.NotNull(result);
        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task DuplicateActiveAssignments_FailClosed()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        db.UserRoleAssignments.Add(CreateActive(userId));
        db.UserRoleAssignments.Add(CreateActive(userId));
        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        Assert.Null(await reader.FindAssignmentAsync(userId));
    }

    [Fact]
    public async Task CancelledLookup_ThrowsCancellation()
    {
        using var db = CreateContext();
        var reader = new SqlTrustedAdministratorRoleReader(db);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.FindAssignmentAsync(
                Guid.NewGuid().ToString("D"),
                cts.Token));
    }

    [Fact]
    public async Task RevocationIsObservedOnNextLookup()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        var assignment = CreateActive(userId);
        db.UserRoleAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        Assert.NotNull(
            await reader.FindAssignmentAsync(userId));

        assignment.RevokeTrusted(
            DateTimeOffset.UtcNow.AddMinutes(1),
            null);

        await db.SaveChangesAsync();

        Assert.Null(
            await reader.FindAssignmentAsync(userId));
    }

    [Fact]
    public async Task InactiveAssignment_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        var assignment = CreateActive(userId);

        db.UserRoleAssignments.Add(assignment);

        db.Entry(assignment)
            .Property(x => x.Status)
            .CurrentValue = "Inactive";

        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        Assert.Null(await reader.FindAssignmentAsync(userId));
    }

    [Fact]
    public async Task WrongRole_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        var assignment = CreateActive(userId);

        db.UserRoleAssignments.Add(assignment);

        db.Entry(assignment)
            .Property(x => x.Role)
            .CurrentValue = "Moderator";

        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        Assert.Null(await reader.FindAssignmentAsync(userId));
    }

    [Fact]
    public async Task ActiveRoleWithRevocationTimestamp_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid().ToString("D");

        var assignment = CreateActive(userId);

        db.UserRoleAssignments.Add(assignment);

        db.Entry(assignment)
            .Property(x => x.RevokedAtUtc)
            .CurrentValue = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        Assert.Null(await reader.FindAssignmentAsync(userId));
    }

    [Fact]
    public async Task UnavailableContext_FailsClosed()
    {
        var db = CreateContext();

        var reader = new SqlTrustedAdministratorRoleReader(db);

        db.Dispose();

        var result = await reader.FindAssignmentAsync(
            Guid.NewGuid().ToString("D"));

        Assert.Null(result);
    }
}