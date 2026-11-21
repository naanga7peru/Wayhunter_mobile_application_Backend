
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
using Wayhunter.Domain.Modules.TripSafety.Enums;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3InvitationDecisionSqlTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M3_RevocationIntegrationTest";

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DecisionAt =
        CreatedAt.AddMinutes(10);

    private static string GetConnectionString() =>
        M3LocalDbProfile.Validate(Environment.GetEnvironmentVariable("WAYHUNTER_M3_TEST_SQL_CONNECTION"),
            AllowedDatabaseName).ConnectionString;

    private static WayhunterSharedDbContext CreateContext(
        DbTransactionInterceptor? interceptor = null)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    GetConnectionString(),
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: new[] { 1205 });
                    });

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new WayhunterSharedDbContext(
            optionsBuilder.Options);
    }

    private sealed record TestSeed(
        Guid TripId,
        Guid InvitationId,
        string OwnerUserId,
        string RecipientUserId);

    private static async Task<TestSeed> SeedAsync()
    {
        // Use the existing isolated M3 SQL test schema.
        // Never apply shared production migrations.
        await M3MembershipRevocationSqlConcurrencyTests
            .EnsureSchemaAsync();

        var owner = UserAccount.RegisterPending(
            "M3 Invitation SQL Owner",
            $"m3-invite-owner-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        var recipient = UserAccount.RegisterPending(
            "M3 Invitation SQL Recipient",
            $"m3-invite-recipient-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        owner.ActivateAfterVerifiedOtp(CreatedAt);
        recipient.ActivateAfterVerifiedOtp(CreatedAt);

        var trip = new TripPlan(
            owner.UserId,
            "M3 Invitation SQL Verification",
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

        await using var context = CreateContext();

        context.Users.AddRange(owner, recipient);
        context.Trips.Add(trip);
        context.TripInvitations.Add(invitation);

        await context.SaveChangesAsync();

        return new TestSeed(
            trip.Id,
            invitation.Id,
            owner.UserId,
            recipient.UserId);
    }

    private static async Task CleanupAsync(TestSeed seed)
    {
        await using var context = CreateContext();

        await context.TripMembers
            .Where(x => x.TripPlanId == seed.TripId)
            .ExecuteDeleteAsync();

        await context.TripInvitations
            .Where(x => x.TripPlanId == seed.TripId)
            .ExecuteDeleteAsync();

        await context.Trips
            .Where(x => x.Id == seed.TripId)
            .ExecuteDeleteAsync();

        await context.Users
            .Where(x =>
                x.UserId == seed.OwnerUserId ||
                x.UserId == seed.RecipientUserId)
            .ExecuteDeleteAsync();
    }

    [M3RevocationSqlFact]
    public async Task Accept_ShouldPersistInvitationAndMembership()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var repository =
                    new EfTripInvitationDecisionRepository(context);

                var invitation = await repository.GetByIdAsync(
                    seed.InvitationId);

                Assert.NotNull(invitation);

                invitation.Accept(DecisionAt);

                var member = TripMember.FromAcceptedInvitation(
                    Guid.NewGuid(),
                    invitation);

                await repository.SaveDecisionAsync(
                    invitation,
                    member);
            }

            await using var verifyContext = CreateContext();

            var storedInvitation =
                await verifyContext.TripInvitations
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == seed.InvitationId);

            Assert.Equal(
                TripInvitationStatus.Accepted,
                storedInvitation.Status);

            Assert.Equal(
                DecisionAt,
                storedInvitation.RespondedAtUtc);

            var storedMember =
                await verifyContext.TripMembers
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.InvitationId == seed.InvitationId);

            Assert.Equal(
                seed.TripId,
                storedMember.TripPlanId);

            Assert.Equal(
                seed.RecipientUserId,
                storedMember.MemberUserId);

            Assert.True(storedMember.IsActive);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M3RevocationSqlFact]
    public async Task Reject_ShouldNotCreateMembership()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var repository =
                    new EfTripInvitationDecisionRepository(context);

                var invitation = await repository.GetByIdAsync(
                    seed.InvitationId);

                Assert.NotNull(invitation);

                invitation.Reject(DecisionAt);

                await repository.SaveDecisionAsync(
                    invitation,
                    null);
            }

            await using var verifyContext = CreateContext();

            var storedInvitation =
                await verifyContext.TripInvitations
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == seed.InvitationId);

            Assert.Equal(
                TripInvitationStatus.Rejected,
                storedInvitation.Status);

            Assert.Equal(
                DecisionAt,
                storedInvitation.RespondedAtUtc);

            var memberCount =
                await verifyContext.TripMembers
                    .CountAsync(
                        x => x.InvitationId == seed.InvitationId);

            Assert.Equal(0, memberCount);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M3RevocationSqlFact]
    public async Task RepeatedAccept_ShouldNotDuplicateMembership()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var repository =
                    new EfTripInvitationDecisionRepository(context);

                var invitation = await repository.GetByIdAsync(
                    seed.InvitationId);

                Assert.NotNull(invitation);

                invitation.Accept(DecisionAt);

                var member = TripMember.FromAcceptedInvitation(
                    Guid.NewGuid(),
                    invitation);

                await repository.SaveDecisionAsync(
                    invitation,
                    member);
            }

            await using (var context = CreateContext())
            {
                var repository =
                    new EfTripInvitationDecisionRepository(context);

                var savedInvitation =
                    await repository.GetByIdAsync(
                        seed.InvitationId);

                Assert.NotNull(savedInvitation);

                var duplicateMember =
                    TripMember.FromAcceptedInvitation(
                        Guid.NewGuid(),
                        savedInvitation);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.SaveDecisionAsync(
                        savedInvitation,
                        duplicateMember));
            }

            await using var verifyContext = CreateContext();

            var memberCount =
                await verifyContext.TripMembers
                    .CountAsync(
                        x => x.InvitationId == seed.InvitationId);

            Assert.Equal(1, memberCount);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

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
                "Injected M3 invitation commit failure.");
        }
    }

    [M3RevocationSqlFact]
    public async Task CommitFailure_ShouldRollbackInvitationAndMember()
    {
        var seed = await SeedAsync();

        try
        {
            var interceptor = new FailCommitInterceptor();

            await using (var context = CreateContext(interceptor))
            {
                var repository =
                    new EfTripInvitationDecisionRepository(context);

                var invitation = await repository.GetByIdAsync(
                    seed.InvitationId);

                Assert.NotNull(invitation);

                invitation.Accept(DecisionAt);

                var member = TripMember.FromAcceptedInvitation(
                    Guid.NewGuid(),
                    invitation);

                var exception =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => repository.SaveDecisionAsync(
                            invitation,
                            member));

                Assert.Contains(
                    "Injected M3 invitation commit failure",
                    exception.ToString());
            }

            Assert.True(interceptor.CommitAttempted);

            // Check SQL state using a fresh context.
            await using (var verifyContext = CreateContext())
            {
                var invitation =
                    await verifyContext.TripInvitations
                        .AsNoTracking()
                        .SingleAsync(
                            x => x.Id == seed.InvitationId);

                Assert.Equal(
                    TripInvitationStatus.Pending,
                    invitation.Status);

                Assert.Null(invitation.RespondedAtUtc);

                var memberCount =
                    await verifyContext.TripMembers
                        .CountAsync(
                            x => x.InvitationId == seed.InvitationId);

                Assert.Equal(0, memberCount);
            }

            // Retry the decision without failure injection.
            await using (var retryContext = CreateContext())
            {
                var repository =
                    new EfTripInvitationDecisionRepository(
                        retryContext);

                var invitation =
                    await repository.GetByIdAsync(
                        seed.InvitationId);

                Assert.NotNull(invitation);

                invitation.Accept(DecisionAt);

                var member = TripMember.FromAcceptedInvitation(
                    Guid.NewGuid(),
                    invitation);

                await repository.SaveDecisionAsync(
                    invitation,
                    member);
            }

            await using var finalContext = CreateContext();

            var savedInvitation =
                await finalContext.TripInvitations
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == seed.InvitationId);

            Assert.Equal(
                TripInvitationStatus.Accepted,
                savedInvitation.Status);

            var savedMembers =
                await finalContext.TripMembers
                    .AsNoTracking()
                    .Where(
                        x => x.InvitationId == seed.InvitationId)
                    .ToListAsync();

            Assert.Single(savedMembers);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }
}
