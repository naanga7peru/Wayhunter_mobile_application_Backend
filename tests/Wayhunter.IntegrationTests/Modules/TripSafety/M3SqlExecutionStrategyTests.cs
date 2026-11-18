
using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3SqlExecutionStrategyTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M3_RevocationIntegrationTest";

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(
            "WAYHUNTER_M3_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Dedicated M3 SQL connection is missing.");
        }

        var builder = new SqlConnectionStringBuilder(value);

        if (!string.Equals(
            builder.InitialCatalog,
            AllowedDatabaseName,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the dedicated M3 integration test database is allowed.");
        }

        return value;
    }

    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    GetConnectionString(),
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 3))
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    [M3RevocationSqlFact]
    public async Task RetryStrategy_ShouldAllowAtomicRevocation()
    {
        await M3MembershipRevocationSqlConcurrencyTests
            .EnsureSchemaAsync();

        var owner = UserAccount.RegisterPending(
            "M3 Retry Test Owner",
            $"m3-retry-owner-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        var recipient = UserAccount.RegisterPending(
            "M3 Retry Test Member",
            $"m3-retry-member-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        owner.ActivateAfterVerifiedOtp(CreatedAt);
        recipient.ActivateAfterVerifiedOtp(CreatedAt);

        var trip = new TripPlan(
            owner.UserId,
            "M3 Execution Strategy Trip",
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

        // Seed data using a separate DbContext.
        await using (var seedContext = CreateContext())
        {
            seedContext.Users.AddRange(owner, recipient);
            seedContext.Trips.Add(trip);
            seedContext.TripInvitations.Add(invitation);
            seedContext.TripMembers.Add(member);

            await seedContext.SaveChangesAsync();
        }

        try
        {
            await using (var context = CreateContext())
            {
                Assert.True(
                    context.Database
                        .CreateExecutionStrategy()
                        .RetriesOnFailure);

                var repository =
                    new EfTripMembershipRevocationRepository(
                        context);

                var result = await repository
                    .RemoveMemberAndQueueNotificationAsync(
                        trip.Id,
                        member.Id,
                        RemovedAt);

                Assert.True(result);
            }

            // Verify committed SQL state.
            await using var verifyContext = CreateContext();

            var savedMember =
                await verifyContext.TripMembers
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == member.Id);

            Assert.False(savedMember.IsActive);
            Assert.Equal(
                RemovedAt,
                savedMember.RemovedAtUtc);

            var notifications =
                await verifyContext
                    .TripMembershipRevocationOutbox
                    .AsNoTracking()
                    .Where(x => x.MemberId == member.Id)
                    .ToListAsync();

            var notification = Assert.Single(notifications);

            Assert.Equal(trip.Id, notification.TripPlanId);
            Assert.Equal(member.Id, notification.MemberId);
            Assert.False(notification.IsDelivered);
        }
        finally
        {
            // Delete only records created by this test.
            await using var cleanup = CreateContext();

            await cleanup.TripMembershipRevocationOutbox
                .Where(x => x.TripPlanId == trip.Id)
                .ExecuteDeleteAsync();

            await cleanup.TripMembers
                .Where(x => x.TripPlanId == trip.Id)
                .ExecuteDeleteAsync();

            await cleanup.TripInvitations
                .Where(x => x.TripPlanId == trip.Id)
                .ExecuteDeleteAsync();

            await cleanup.Trips
                .Where(x => x.Id == trip.Id)
                .ExecuteDeleteAsync();

            await cleanup.Users
                .Where(x =>
                    x.UserId == owner.UserId ||
                    x.UserId == recipient.UserId)
                .ExecuteDeleteAsync();
        }
    }
}
