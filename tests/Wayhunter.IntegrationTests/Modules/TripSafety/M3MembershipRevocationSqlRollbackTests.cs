
using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3MembershipRevocationSqlRollbackTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M3_RevocationIntegrationTest";

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private static string GetConnectionString() =>
        M3LocalDbProfile.Validate(Environment.GetEnvironmentVariable("WAYHUNTER_M3_TEST_SQL_CONNECTION"),
            AllowedDatabaseName).ConnectionString;

    private static WayhunterSharedDbContext CreateContext(
        DbTransactionInterceptor? interceptor = null)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(GetConnectionString());

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new WayhunterSharedDbContext(
            optionsBuilder.Options);
    }

    private static async Task<(
        Guid TripId,
        Guid MemberId,
        string OwnerUserId,
        string MemberUserId)> SeedAsync()
    {
        // Create only the dedicated M3 test schema.
        await M3MembershipRevocationSqlConcurrencyTests
            .EnsureSchemaAsync();

        var owner = UserAccount.RegisterPending(
            "M3 Rollback Test Owner",
            $"m3-rollback-owner-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        var recipient = UserAccount.RegisterPending(
            "M3 Rollback Test Member",
            $"m3-rollback-member-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        owner.ActivateAfterVerifiedOtp(CreatedAt);
        recipient.ActivateAfterVerifiedOtp(CreatedAt);

        var trip = new TripPlan(
            owner.UserId,
            "M3 SQL Rollback Trip",
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

    // Inject a failure immediately before SQL commit.
    // Disposing the uncommitted transaction rolls back
    // membership changes and the outbox insertion.
    private sealed class FailCommitInterceptor
        : DbTransactionInterceptor
    {
        public bool CommitAttempted { get; private set; }

        public override ValueTask<InterceptionResult>
            TransactionCommittingAsync(
                DbTransaction transaction,
                TransactionEventData eventData,
                InterceptionResult result,
                CancellationToken cancellationToken = default)
        {
            CommitAttempted = true;

            throw new InvalidOperationException(
                "Injected M3 transaction commit failure.");
        }
    }

    [M3RevocationSqlFact]
    public async Task CommitFailure_ShouldRollbackMembershipAndOutbox()
    {
        var seed = await SeedAsync();

        try
        {
            var interceptor = new FailCommitInterceptor();

            // Attempt removal, but deliberately fail
            // immediately before transaction commit.
            await using (var context = CreateContext(interceptor))
            {
                var repository =
                    new EfTripMembershipRevocationRepository(
                        context);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository
                        .RemoveMemberAndQueueNotificationAsync(
                            seed.TripId,
                            seed.MemberId,
                            RemovedAt));
            }

            Assert.True(interceptor.CommitAttempted);

            // Fresh context: failed transaction must
            // not have changed the database.
            await using (var verifyContext = CreateContext())
            {
                var member = await verifyContext.TripMembers
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == seed.MemberId);

                Assert.True(member.IsActive);
                Assert.Null(member.RemovedAtUtc);

                var notificationCount =
                    await verifyContext.TripMembershipRevocationOutbox
                        .CountAsync(x => x.MemberId == seed.MemberId);

                Assert.Equal(0, notificationCount);
            }

            // Retry without failure injection.
            // The removal should succeed normally.
            await using (var retryContext = CreateContext())
            {
                var repository =
                    new EfTripMembershipRevocationRepository(
                        retryContext);

                var removed = await repository
                    .RemoveMemberAndQueueNotificationAsync(
                        seed.TripId,
                        seed.MemberId,
                        RemovedAt);

                Assert.True(removed);
            }

            // Final database verification.
            await using (var finalContext = CreateContext())
            {
                var member = await finalContext.TripMembers
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == seed.MemberId);

                Assert.False(member.IsActive);
                Assert.Equal(RemovedAt, member.RemovedAtUtc);

                var notifications =
                    await finalContext.TripMembershipRevocationOutbox
                        .AsNoTracking()
                        .Where(x => x.MemberId == seed.MemberId)
                        .ToListAsync();

                var notification = Assert.Single(notifications);

                Assert.Equal(
                    seed.TripId,
                    notification.TripPlanId);

                Assert.Equal(
                    seed.MemberId,
                    notification.MemberId);

                Assert.False(notification.IsDelivered);
                Assert.Null(notification.DeliveredAtUtc);
                Assert.Equal(0, notification.DeliveryAttempts);
            }
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
