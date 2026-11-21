
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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

public sealed class M3GpsSqlFactAttribute : FactAttribute
{
    public M3GpsSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M3_GPS_TEST_SQL_CONNECTION")))
        {
            Skip = "Dedicated M3 GPS SQL test DB is not configured.";
        }
    }
}

public sealed class M3GpsConsentSqlTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M3_GpsConsentIntegrationTest";

    private static readonly SemaphoreSlim SchemaGate = new(1, 1);
    private static bool _schemaReady;

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset StartedAt =
        CreatedAt.AddMinutes(10);

    private static readonly DateTimeOffset GrantAt =
        CreatedAt.AddHours(1);

    private static readonly DateTimeOffset RevokeAt =
        CreatedAt.AddHours(2);

    private static readonly DateTimeOffset RemoveAt =
        CreatedAt.AddHours(3);

    private static string GetConnectionString() =>
        M3LocalDbProfile.Validate(Environment.GetEnvironmentVariable("WAYHUNTER_M3_GPS_TEST_SQL_CONNECTION"),
            AllowedDatabaseName).ConnectionString;

    private static WayhunterSharedDbContext CreateContext(
        DbTransactionInterceptor? interceptor = null)
    {
        var builder =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    GetConnectionString(),
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: new[] { 1205 }));

        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new WayhunterSharedDbContext(builder.Options);
    }

    // Dedicated SQL test schema.
    // No shared migrations or production database changes.

    private sealed class GpsSqlTestSchemaContext : DbContext
    {
        public GpsSqlTestSchemaContext(
            DbContextOptions<GpsSqlTestSchemaContext> options)
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
                new LiveTripSessionConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripInvitationConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripMemberConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripGpsConsentConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripMembershipRevocationOutboxConfiguration());
        }
    }

    private static async Task EnsureSchemaAsync()
    {
        await SchemaGate.WaitAsync();

        try
        {
            if (_schemaReady)
            {
                return;
            }

            var options =
                new DbContextOptionsBuilder<GpsSqlTestSchemaContext>()
                    .UseSqlServer(GetConnectionString())
                    .Options;

            await using var context =
                new GpsSqlTestSchemaContext(options);

            await context.Database.EnsureCreatedAsync();

            _schemaReady = true;
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    private sealed record TestSeed(
        Guid TripId,
        Guid SessionId,
        Guid MemberId,
        string OwnerUserId,
        string MemberUserId,
        string OutsiderUserId);

    private static async Task<TestSeed> SeedAsync()
    {
        await EnsureSchemaAsync();

        var owner = UserAccount.RegisterPending(
            "M3 GPS Test Owner",
            $"m3-gps-owner-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        var recipient = UserAccount.RegisterPending(
            "M3 GPS Test Member",
            $"m3-gps-member-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        var outsider = UserAccount.RegisterPending(
            "M3 GPS Test Outsider",
            $"m3-gps-outsider-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        owner.ActivateAfterVerifiedOtp(CreatedAt);
        recipient.ActivateAfterVerifiedOtp(CreatedAt);
        outsider.ActivateAfterVerifiedOtp(CreatedAt);

        var trip = new TripPlan(
            owner.UserId,
            "M3 GPS Consent SQL Trip",
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

        var session = LiveTripSession.Start(
            Guid.NewGuid(),
            trip.Id,
            StartedAt);

        await using var context = CreateContext();

        context.Users.AddRange(owner, recipient, outsider);
        context.Trips.Add(trip);
        context.TripInvitations.Add(invitation);
        context.TripMembers.Add(member);
        context.LiveTripSessions.Add(session);

        // Invitation acceptance must not create GPS consent.
        await context.SaveChangesAsync();

        return new TestSeed(
            trip.Id,
            session.SessionId,
            member.Id,
            owner.UserId,
            recipient.UserId,
            outsider.UserId);
    }

    private static async Task CleanupAsync(TestSeed seed)
    {
        await using var context = CreateContext();

        await context.TripMembershipRevocationOutbox
            .Where(x => x.TripPlanId == seed.TripId)
            .ExecuteDeleteAsync();

        await context.TripGpsConsents
            .Where(x => x.SessionId == seed.SessionId)
            .ExecuteDeleteAsync();

        await context.LiveTripSessions
            .Where(x => x.SessionId == seed.SessionId)
            .ExecuteDeleteAsync();

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
            .Where(x => x.UserId == seed.OwnerUserId ||
                        x.UserId == seed.MemberUserId ||
                        x.UserId == seed.OutsiderUserId)
            .ExecuteDeleteAsync();
    }

    // TEST 01
    // Accepted invitation must not grant GPS automatically.

    [M3GpsSqlFact]
    public async Task AcceptedInvitation_ShouldNotAutoGrantGps()
    {
        var seed = await SeedAsync();

        try
        {
            await using var context = CreateContext();

            var consentCount = await context.TripGpsConsents
                .CountAsync(x => x.SessionId == seed.SessionId);

            Assert.Equal(0, consentCount);

            var accessRepository =
                new EfTripSharingConsentRepository(context);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => accessRepository.EnsureAllowedAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 02
    // Explicit GPS grant must persist in SQL.

    [M3GpsSqlFact]
    public async Task Grant_ShouldPersistAndAllowLocationAccess()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var grantRepository =
                    new EfTripSharingConsentRepository(context);

                var result = await grantRepository.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    GrantAt);

                Assert.True(result.IsGranted);
                Assert.Equal(GrantAt, result.GrantedAtUtc);
                Assert.Null(result.RevokedAtUtc);
            }

            await using var verifyContext = CreateContext();

            var saved = await verifyContext.TripGpsConsents
                .AsNoTracking()
                .SingleAsync(x =>
                    x.SessionId == seed.SessionId &&
                    x.UserId == seed.MemberUserId);

            Assert.True(saved.IsGranted);
            Assert.Equal(GrantAt, saved.GrantedAtUtc);
            Assert.Null(saved.RevokedAtUtc);

            var accessRepository =
                new EfTripSharingConsentRepository(verifyContext);

            await accessRepository.EnsureAllowedAsync(
                seed.TripId,
                seed.SessionId,
                seed.MemberUserId);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 03
    // Revocation must persist and block future GPS access.

    [M3GpsSqlFact]
    public async Task Revoke_ShouldPersistAndDenyLocationAccess()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var grantRepository =
                    new EfTripSharingConsentRepository(context);

                await grantRepository.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    GrantAt);
            }

            await using (var context = CreateContext())
            {
                var revokeRepository =
                    new EfTripSharingConsentRepository(context);

                var result = await revokeRepository.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    false,
                    RevokeAt);

                Assert.False(result.IsGranted);
                Assert.Equal(RevokeAt, result.RevokedAtUtc);
            }

            await using var verifyContext = CreateContext();

            var saved = await verifyContext.TripGpsConsents
                .AsNoTracking()
                .SingleAsync(x =>
                    x.SessionId == seed.SessionId &&
                    x.UserId == seed.MemberUserId);

            Assert.False(saved.IsGranted);
            Assert.Equal(RevokeAt, saved.RevokedAtUtc);

            var accessRepository =
                new EfTripSharingConsentRepository(verifyContext);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => accessRepository.EnsureAllowedAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 04
    // Non-members cannot create GPS consent.

    [M3GpsSqlFact]
    public async Task NonMember_ShouldNotCreateGpsConsent()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var consentRepository =
                    new EfTripSharingConsentRepository(context);

                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => consentRepository.SetMyConsentAsync(
                        seed.TripId,
                        seed.SessionId,
                        seed.OutsiderUserId,
                        true,
                        GrantAt));
            }

            await using var verifyContext = CreateContext();

            var count = await verifyContext.TripGpsConsents
                .CountAsync(x => x.SessionId == seed.SessionId);

            Assert.Equal(0, count);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 05
    // Wrong trip and session must be rejected.

    [M3GpsSqlFact]
    public async Task WrongTripOrSession_ShouldDenyConsent()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var consentRepository =
                    new EfTripSharingConsentRepository(context);

                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => consentRepository.SetMyConsentAsync(
                        Guid.NewGuid(),
                        seed.SessionId,
                        seed.MemberUserId,
                        true,
                        GrantAt));

                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => consentRepository.SetMyConsentAsync(
                        seed.TripId,
                        Guid.NewGuid(),
                        seed.MemberUserId,
                        true,
                        GrantAt));
            }

            await using var verifyContext = CreateContext();

            var count = await verifyContext.TripGpsConsents
                .CountAsync(x => x.SessionId == seed.SessionId);

            Assert.Equal(0, count);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 06
    // Injected commit failure must rollback GPS consent.

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
                "Injected M3 GPS consent commit failure.");
        }
    }

    [M3GpsSqlFact]
    public async Task CommitFailure_ShouldRollbackGpsConsent()
    {
        var seed = await SeedAsync();

        try
        {
            var interceptor = new FailCommitInterceptor();

            await using (var context = CreateContext(interceptor))
            {
                var failingRepository =
                    new EfTripSharingConsentRepository(context);

                var exception =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => failingRepository.SetMyConsentAsync(
                            seed.TripId,
                            seed.SessionId,
                            seed.MemberUserId,
                            true,
                            GrantAt));

                Assert.Contains(
                    "Injected M3 GPS consent commit failure",
                    exception.ToString());
            }

            Assert.True(interceptor.CommitAttempted);

            // Fresh SQL context verifies rollback.
            await using (var verifyContext = CreateContext())
            {
                var count = await verifyContext.TripGpsConsents
                    .CountAsync(x =>
                        x.SessionId == seed.SessionId &&
                        x.UserId == seed.MemberUserId);

                Assert.Equal(0, count);
            }

            // Retry without injected failure.
            await using (var retryContext = CreateContext())
            {
                var retryRepository =
                    new EfTripSharingConsentRepository(retryContext);

                var result = await retryRepository.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    GrantAt);

                Assert.True(result.IsGranted);
            }
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 07
    // Removed member must lose access even if old consent remains.

    [M3GpsSqlFact]
    public async Task MemberRemoval_ShouldDenyGpsAndRegrant()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var grantRepository =
                    new EfTripSharingConsentRepository(context);

                await grantRepository.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    GrantAt);
            }

            await using (var context = CreateContext())
            {
                var membershipRepository =
                    new EfTripMembershipRevocationRepository(context);

                var removed = await membershipRepository
                    .RemoveMemberAndQueueNotificationAsync(
                        seed.TripId,
                        seed.MemberId,
                        RemoveAt);

                Assert.True(removed);
            }

            await using var verifyContext = CreateContext();

            var member = await verifyContext.TripMembers
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.MemberId);

            Assert.False(member.IsActive);
            Assert.Equal(RemoveAt, member.RemovedAtUtc);

            var consent = await verifyContext.TripGpsConsents
                .AsNoTracking()
                .SingleAsync(x =>
                    x.SessionId == seed.SessionId &&
                    x.UserId == seed.MemberUserId);

            // Historical consent remains, but access must be denied.
            Assert.True(consent.IsGranted);

            var accessRepository =
                new EfTripSharingConsentRepository(verifyContext);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => accessRepository.EnsureAllowedAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => accessRepository.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    RemoveAt.AddMinutes(1)));

            var outboxCount =
                await verifyContext.TripMembershipRevocationOutbox
                    .CountAsync(x => x.MemberId == seed.MemberId);

            Assert.Equal(1, outboxCount);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 08
    // Finished trips must reject consent changes.

    [M3GpsSqlFact]
    public async Task FinishedSession_ShouldRejectConsentChange()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var session = await context.LiveTripSessions
                    .SingleAsync(x => x.SessionId == seed.SessionId);

                session.Finish(GrantAt);

                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var consentRepository =
                    new EfTripSharingConsentRepository(context);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => consentRepository.SetMyConsentAsync(
                        seed.TripId,
                        seed.SessionId,
                        seed.MemberUserId,
                        true,
                        GrantAt.AddMinutes(1)));
            }

            await using var verifyContext = CreateContext();

            var count = await verifyContext.TripGpsConsents
                .CountAsync(x => x.SessionId == seed.SessionId);

            Assert.Equal(0, count);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 09
    // Paused trips must not publish live GPS locations.

    [M3GpsSqlFact]
    public async Task PausedSession_ShouldDenyLocationPublishing()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var session = await context.LiveTripSessions
                    .SingleAsync(x => x.SessionId == seed.SessionId);

                session.Pause();

                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var consentRepository =
                    new EfTripSharingConsentRepository(context);

                await consentRepository.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    GrantAt);
            }

            await using var verifyContext = CreateContext();

            var accessRepository =
                new EfTripSharingConsentRepository(verifyContext);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => accessRepository.EnsureAllowedAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // TEST 10
    // Repeated grant/revoke must not create duplicates.

    [M3GpsSqlFact]
    public async Task RepeatedGrantRevoke_ShouldRemainIdempotent()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var context = CreateContext())
            {
                var firstGrant =
                    new EfTripSharingConsentRepository(context);

                await firstGrant.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    GrantAt);
            }

            await using (var context = CreateContext())
            {
                var repeatedGrant =
                    new EfTripSharingConsentRepository(context);

                await repeatedGrant.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    true,
                    GrantAt.AddMinutes(1));
            }

            await using (var context = CreateContext())
            {
                var firstRevoke =
                    new EfTripSharingConsentRepository(context);

                await firstRevoke.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    false,
                    RevokeAt);
            }

            await using (var context = CreateContext())
            {
                var repeatedRevoke =
                    new EfTripSharingConsentRepository(context);

                await repeatedRevoke.SetMyConsentAsync(
                    seed.TripId,
                    seed.SessionId,
                    seed.MemberUserId,
                    false,
                    RevokeAt.AddMinutes(1));
            }

            await using var verifyContext = CreateContext();

            var consents = await verifyContext.TripGpsConsents
                .AsNoTracking()
                .Where(x =>
                    x.SessionId == seed.SessionId &&
                    x.UserId == seed.MemberUserId)
                .ToListAsync();

            var consent = Assert.Single(consents);

            Assert.False(consent.IsGranted);
            Assert.Equal(GrantAt, consent.GrantedAtUtc);
            Assert.Equal(RevokeAt, consent.RevokedAtUtc);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }
}
