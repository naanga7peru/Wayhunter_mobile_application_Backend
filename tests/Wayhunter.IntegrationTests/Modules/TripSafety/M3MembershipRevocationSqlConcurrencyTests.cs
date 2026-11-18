
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

// Skip SQL-only tests when no dedicated connection exists.
public sealed class M3RevocationSqlFactAttribute : FactAttribute
{
    public M3RevocationSqlFactAttribute()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M3_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = "Dedicated M3 SQL test database is not configured.";
        }
    }
}

public sealed class M3MembershipRevocationSqlConcurrencyTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M3_RevocationIntegrationTest";

    private static readonly SemaphoreSlim SchemaGate =
        new(1, 1);

    private static bool _schemaInitialized;

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private static string GetConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M3_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "M3 SQL test connection is missing.");
        }

        var builder =
            new SqlConnectionStringBuilder(connectionString);

        if (!string.Equals(
            builder.InitialCatalog,
            AllowedDatabaseName,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the dedicated M3 integration test database is allowed.");
        }

        return connectionString;
    }

    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(GetConnectionString())
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    // Only M1, M2 and M3 tables needed by these tests.
    // Never run shared production migrations here.
    private sealed class M3TestSchemaContext : DbContext
    {
        public M3TestSchemaContext(
            DbContextOptions<M3TestSchemaContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(
                new UserAccountConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripPlanConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripStopConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripInvitationConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripMemberConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripMembershipRevocationOutboxConfiguration());
        }
    }

    // Also used by the separate rollback test class.
    internal static async Task EnsureSchemaAsync()
    {
        await SchemaGate.WaitAsync();

        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            var options =
                new DbContextOptionsBuilder<M3TestSchemaContext>()
                    .UseSqlServer(GetConnectionString())
                    .Options;

            await using var context =
                new M3TestSchemaContext(options);

            await context.Database.EnsureCreatedAsync();

            _schemaInitialized = true;
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    private static async Task<(
        Guid TripId,
        Guid MemberId,
        string OwnerUserId,
        string MemberUserId)> SeedAsync()
    {
        await EnsureSchemaAsync();

        var owner = UserAccount.RegisterPending(
            "M3 SQL Test Owner",
            $"m3-owner-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        var recipient = UserAccount.RegisterPending(
            "M3 SQL Test Member",
            $"m3-member-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        owner.ActivateAfterVerifiedOtp(CreatedAt);
        recipient.ActivateAfterVerifiedOtp(CreatedAt);

        var trip = new TripPlan(
            owner.UserId,
            "M3 SQL Concurrency Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Van",
            5);

        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            trip.Id,
            owner.UserId,
            recipient.UserId,
            CreatedAt);

        invitation.Accept(JoinedAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        await using var context = CreateContext();

        context.Users.AddRange(owner, recipient);
        context.Trips.Add(trip);
        context.TripInvitations.Add(invitation);
        context.TripMembers.Add(member);

        await context.SaveChangesAsync();

        return (
            trip.Id,
            member.Id,
            owner.UserId,
            recipient.UserId);
    }

    private static async Task CleanupAsync(
        Guid tripId,
        string ownerUserId,
        string memberUserId)
    {
        await using var context = CreateContext();

        await context.TripMembershipRevocationOutbox
            .Where(x => x.TripPlanId == tripId)
            .ExecuteDeleteAsync();

        await context.TripMembers
            .Where(x => x.TripPlanId == tripId)
            .ExecuteDeleteAsync();

        await context.TripInvitations
            .Where(x => x.TripPlanId == tripId)
            .ExecuteDeleteAsync();

        await context.Trips
            .Where(x => x.Id == tripId)
            .ExecuteDeleteAsync();

        await context.Users
            .Where(x =>
                x.UserId == ownerUserId ||
                x.UserId == memberUserId)
            .ExecuteDeleteAsync();
    }

    [M3RevocationSqlFact]
    public async Task ConcurrentRemoval_ExactlyOne_ShouldSucceed()
    {
        var seed = await SeedAsync();

        try
        {
            async Task<bool> RemoveAsync()
            {
                // Each request uses an independent DbContext.
                await using var context = CreateContext();

                var repository =
                    new EfTripMembershipRevocationRepository(context);

                return await repository
                    .RemoveMemberAndQueueNotificationAsync(
                        seed.TripId,
                        seed.MemberId,
                        RemovedAt);
            }

            var results = await Task.WhenAll(
                RemoveAsync(),
                RemoveAsync());

            Assert.Single(results, result => result);
            Assert.Single(results, result => !result);

            await using var verifyContext = CreateContext();

            var member = await verifyContext.TripMembers
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.MemberId);

            Assert.False(member.IsActive);
            Assert.Equal(RemovedAt, member.RemovedAtUtc);

            var notifications =
                await verifyContext.TripMembershipRevocationOutbox
                    .AsNoTracking()
                    .Where(x => x.MemberId == seed.MemberId)
                    .ToListAsync();

            var notification = Assert.Single(notifications);

            Assert.Equal(seed.TripId, notification.TripPlanId);
            Assert.Equal(seed.MemberId, notification.MemberId);
            Assert.Equal(
                seed.MemberUserId,
                notification.MemberUserId);

            Assert.False(notification.IsDelivered);
            Assert.Null(notification.DeliveredAtUtc);
            Assert.Equal(0, notification.DeliveryAttempts);
        }
        finally
        {
            await CleanupAsync(
                seed.TripId,
                seed.OwnerUserId,
                seed.MemberUserId);
        }
    }

    [M3RevocationSqlFact]
    public async Task RepeatedRemoval_ShouldNotDuplicateOutbox()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var firstContext = CreateContext())
            {
                var repository =
                    new EfTripMembershipRevocationRepository(
                        firstContext);

                var first = await repository
                    .RemoveMemberAndQueueNotificationAsync(
                        seed.TripId,
                        seed.MemberId,
                        RemovedAt);

                Assert.True(first);
            }

            await using (var secondContext = CreateContext())
            {
                var repository =
                    new EfTripMembershipRevocationRepository(
                        secondContext);

                var second = await repository
                    .RemoveMemberAndQueueNotificationAsync(
                        seed.TripId,
                        seed.MemberId,
                        RemovedAt.AddMinutes(10));

                Assert.False(second);
            }

            await using var verifyContext = CreateContext();

            var count =
                await verifyContext.TripMembershipRevocationOutbox
                    .CountAsync(x => x.MemberId == seed.MemberId);

            Assert.Equal(1, count);

            var member = await verifyContext.TripMembers
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.MemberId);

            Assert.False(member.IsActive);
            Assert.Equal(RemovedAt, member.RemovedAtUtc);
        }
        finally
        {
            await CleanupAsync(
                seed.TripId,
                seed.OwnerUserId,
                seed.MemberUserId);
        }
    }
}
