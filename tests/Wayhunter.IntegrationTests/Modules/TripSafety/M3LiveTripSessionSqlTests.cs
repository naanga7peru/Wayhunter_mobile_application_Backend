
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3LiveSessionSqlFactAttribute : FactAttribute
{
    public M3LiveSessionSqlFactAttribute()
    {
        var connection = Environment.GetEnvironmentVariable(
            "WAYHUNTER_M3_SESSION_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connection))
        {
            Skip = "Dedicated M3 live session SQL database is not configured.";
        }
    }
}

public sealed class M3LiveTripSessionSqlTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M3_LiveSessionIntegrationTest";

    private static readonly SemaphoreSlim SchemaGate =
        new(1, 1);

    private static bool _schemaReady;

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset StartedAt =
        CreatedAt.AddHours(1);

    private static readonly DateTimeOffset FinishedAt =
        CreatedAt.AddHours(3);

    // -----------------------------------------------------
    // SQL CONNECTION SAFETY
    // -----------------------------------------------------

    private static string GetConnectionString() =>
        M3LocalDbProfile.Validate(Environment.GetEnvironmentVariable("WAYHUNTER_M3_SESSION_TEST_SQL_CONNECTION"),
            AllowedDatabaseName).ConnectionString;

    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    GetConnectionString(),
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: new[] { 1205 }))
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    // -----------------------------------------------------
    // ISOLATED M3 SQL TEST SCHEMA
    //
    // No shared migration execution.
    // No production database modification.
    // -----------------------------------------------------

    private sealed class SessionSchemaContext : DbContext
    {
        public SessionSchemaContext(
            DbContextOptions<SessionSchemaContext> options)
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
                new DbContextOptionsBuilder<SessionSchemaContext>()
                    .UseSqlServer(GetConnectionString())
                    .Options;

            await using var context =
                new SessionSchemaContext(options);

            await context.Database.EnsureCreatedAsync();

            _schemaReady = true;
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    // -----------------------------------------------------
    // TEST DATA
    // -----------------------------------------------------

    private sealed record TestSeed(
        Guid TripId,
        string OwnerUserId);

    private static async Task<TestSeed> SeedAsync()
    {
        await EnsureSchemaAsync();

        var owner = UserAccount.RegisterPending(
            "M3 Live Session SQL Owner",
            $"m3-session-owner-{Guid.NewGuid():N}@example.invalid",
            "test-password-hash",
            CreatedAt);

        owner.ActivateAfterVerifiedOtp(CreatedAt);

        var trip = new TripPlan(
            owner.UserId,
            "M3 Live Session SQL Verification",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Van",
            5);

        await using var context = CreateContext();

        context.Users.Add(owner);
        context.Trips.Add(trip);

        await context.SaveChangesAsync();

        return new TestSeed(
            trip.Id,
            owner.UserId);
    }

    private static async Task CleanupAsync(TestSeed seed)
    {
        await using var context = CreateContext();

        await context.LiveTripSessions
            .Where(x => x.TripPlanId == seed.TripId)
            .ExecuteDeleteAsync();

        await context.Trips
            .Where(x => x.Id == seed.TripId)
            .ExecuteDeleteAsync();

        await context.Users
            .Where(x => x.UserId == seed.OwnerUserId)
            .ExecuteDeleteAsync();
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }

    private static LiveTripSessionService CreateService(
        WayhunterSharedDbContext context,
        DateTimeOffset now)
    {
        return new LiveTripSessionService(
            new EfLiveTripSessionRepository(context),
            new FixedClock(now));
    }

    private static async Task<Guid> StartSessionAsync(
        Guid tripId)
    {
        await using var context = CreateContext();

        var service = CreateService(
            context,
            StartedAt);

        var session = await service.StartAsync(tripId);

        return session.SessionId;
    }

    // -----------------------------------------------------
    // TEST 01
    // Start should persist an active session in SQL.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task Start_ShouldPersistActiveSession()
    {
        var seed = await SeedAsync();

        try
        {
            var sessionId =
                await StartSessionAsync(seed.TripId);

            await using var context = CreateContext();

            var saved = await context.LiveTripSessions
                .AsNoTracking()
                .SingleAsync(
                    x => x.SessionId == sessionId);

            Assert.Equal(
                seed.TripId,
                saved.TripPlanId);

            Assert.Equal(
                LiveTripStatus.Active,
                saved.Status);

            Assert.Equal(
                StartedAt,
                saved.StartedAtUtc);

            Assert.Null(saved.FinishedAtUtc);

            var sessionRepository =
                new EfLiveTripSessionRepository(context);

            var open = await sessionRepository
                .GetOpenByTripPlanIdAsync(seed.TripId);

            Assert.NotNull(open);

            Assert.Equal(
                sessionId,
                open.SessionId);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 02
    // Repeated Start should return the existing session.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task RepeatedStart_ShouldReturnExistingSession()
    {
        var seed = await SeedAsync();

        try
        {
            var firstId =
                await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    StartedAt.AddMinutes(10));

                var second =
                    await service.StartAsync(seed.TripId);

                Assert.Equal(
                    firstId,
                    second.SessionId);
            }

            await using var verifyContext = CreateContext();

            var count = await verifyContext.LiveTripSessions
                .CountAsync(
                    x => x.TripPlanId == seed.TripId &&
                         x.Status != LiveTripStatus.Finished);

            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 03
    // Pause and Resume should persist across SQL contexts.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task PauseResume_ShouldPersistAcrossContexts()
    {
        var seed = await SeedAsync();

        try
        {
            var sessionId =
                await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    StartedAt);

                var paused =
                    await service.PauseAsync(sessionId);

                Assert.Equal(
                    LiveTripStatus.Paused,
                    paused.Status);
            }

            await using (var verifyContext = CreateContext())
            {
                var saved =
                    await verifyContext.LiveTripSessions
                        .AsNoTracking()
                        .SingleAsync(
                            x => x.SessionId == sessionId);

                Assert.Equal(
                    LiveTripStatus.Paused,
                    saved.Status);
            }

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    StartedAt.AddMinutes(10));

                var resumed =
                    await service.ResumeAsync(sessionId);

                Assert.Equal(
                    LiveTripStatus.Active,
                    resumed.Status);
            }

            await using var finalContext = CreateContext();

            var finalSession =
                await finalContext.LiveTripSessions
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.SessionId == sessionId);

            Assert.Equal(
                LiveTripStatus.Active,
                finalSession.Status);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 04
    // Finish should persist its UTC timestamp.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task Finish_ShouldCloseOpenSession()
    {
        var seed = await SeedAsync();

        try
        {
            var sessionId =
                await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    FinishedAt);

                var finished =
                    await service.FinishAsync(sessionId);

                Assert.Equal(
                    LiveTripStatus.Finished,
                    finished.Status);

                Assert.Equal(
                    FinishedAt,
                    finished.FinishedAtUtc);
            }

            await using var verifyContext = CreateContext();

            var saved =
                await verifyContext.LiveTripSessions
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.SessionId == sessionId);

            Assert.Equal(
                LiveTripStatus.Finished,
                saved.Status);

            Assert.Equal(
                FinishedAt,
                saved.FinishedAtUtc);

            var sessionRepository =
                new EfLiveTripSessionRepository(verifyContext);

            var open = await sessionRepository
                .GetOpenByTripPlanIdAsync(seed.TripId);

            Assert.Null(open);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 05
    // A finished trip should allow another session.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task FinishedSession_ShouldAllowNewSession()
    {
        var seed = await SeedAsync();

        try
        {
            var oldId =
                await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    FinishedAt);

                await service.FinishAsync(oldId);
            }

            Guid newId;

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    FinishedAt.AddMinutes(1));

                var newSession =
                    await service.StartAsync(seed.TripId);

                newId = newSession.SessionId;

                Assert.NotEqual(oldId, newId);
            }

            await using var verifyContext = CreateContext();
            var guard = new EfTripFinishedReminderGuard(verifyContext);
            Assert.False(await guard.CanDispatchAsync(seed.TripId, oldId));
            Assert.True(await guard.CanDispatchAsync(seed.TripId, newId));
            Assert.False(await guard.CanDispatchAsync(seed.TripId, Guid.NewGuid()));
            Assert.False(await guard.CanDispatchAsync(Guid.NewGuid(), newId));

            var sessions =
                await verifyContext.LiveTripSessions
                    .AsNoTracking()
                    .Where(x => x.TripPlanId == seed.TripId)
                    .ToListAsync();

            Assert.Equal(2, sessions.Count);

            // Fixed xUnit analyzer warning:
            // Use Assert.Single(collection, predicate).
            Assert.Single(
                sessions,
                x => x.Status == LiveTripStatus.Finished);

            Assert.Single(
                sessions,
                x => x.Status == LiveTripStatus.Active);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 06
    // Repeated Finish should preserve original timestamp.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task RepeatedFinish_ShouldNotChangeTimestamp()
    {
        var seed = await SeedAsync();

        try
        {
            var sessionId =
                await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    FinishedAt);

                await service.FinishAsync(sessionId);
            }

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    FinishedAt.AddHours(1));

                var result =
                    await service.FinishAsync(sessionId);

                Assert.Equal(
                    LiveTripStatus.Finished,
                    result.Status);

                Assert.Equal(
                    FinishedAt,
                    result.FinishedAtUtc);
            }

            await using var verifyContext = CreateContext();

            var saved =
                await verifyContext.LiveTripSessions
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.SessionId == sessionId);

            Assert.Equal(
                FinishedAt,
                saved.FinishedAtUtc);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 07
    // Duplicate open sessions should be rejected.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task DuplicateOpenSession_ShouldBeRejected()
    {
        var seed = await SeedAsync();

        try
        {
            await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var sessionRepository =
                    new EfLiveTripSessionRepository(context);

                var duplicate = LiveTripSession.Start(
                    Guid.NewGuid(),
                    seed.TripId,
                    StartedAt.AddMinutes(5));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => sessionRepository.AddAsync(duplicate));
            }

            await using var verifyContext = CreateContext();

            var count =
                await verifyContext.LiveTripSessions
                    .CountAsync(
                        x => x.TripPlanId == seed.TripId &&
                             x.Status != LiveTripStatus.Finished);

            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 08
    // SQL filtered unique index must prevent duplicates.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task SqlUniqueIndex_ShouldRejectSecondOpenSession()
    {
        var seed = await SeedAsync();

        try
        {
            await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var duplicate = LiveTripSession.Start(
                    Guid.NewGuid(),
                    seed.TripId,
                    StartedAt.AddMinutes(5));

                // Bypass repository pre-check to test SQL itself.
                context.LiveTripSessions.Add(duplicate);

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => context.SaveChangesAsync());
            }

            await using var verifyContext = CreateContext();

            var count =
                await verifyContext.LiveTripSessions
                    .CountAsync(
                        x => x.TripPlanId == seed.TripId &&
                             x.Status != LiveTripStatus.Finished);

            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // RECOVERY ACCESS CHECKER
    // -----------------------------------------------------

    private sealed class TestRecoveryAccessChecker
        : ITripSessionRecoveryAccessChecker
    {
        private readonly Guid _tripId;
        private readonly string _allowedUserId;

        public TestRecoveryAccessChecker(
            Guid tripId,
            string allowedUserId)
        {
            _tripId = tripId;
            _allowedUserId = allowedUserId;
        }

        public Task<bool> CanRecoverAsync(
            string authenticatedUserId,
            Guid tripPlanId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var authorized =
                tripPlanId == _tripId &&
                string.Equals(
                    authenticatedUserId,
                    _allowedUserId,
                    StringComparison.Ordinal);

            return Task.FromResult(authorized);
        }
    }

    // -----------------------------------------------------
    // TEST 09
    // Authorized recovery works for Active and Paused.
    // Finished sessions should not be recovered.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task Recovery_ShouldReturnOnlyOpenSessions()
    {
        var seed = await SeedAsync();

        try
        {
            var sessionId =
                await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var recovery =
                    new LiveTripSessionRecoveryService(
                        new EfLiveTripSessionRepository(context),
                        new TestRecoveryAccessChecker(
                            seed.TripId,
                            seed.OwnerUserId));

                var active =
                    await recovery.RecoverAsync(
                        sessionId,
                        seed.OwnerUserId);

                Assert.NotNull(active);

                Assert.Equal(
                    LiveTripStatus.Active,
                    active.Status);
            }

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    StartedAt);

                await service.PauseAsync(sessionId);
            }

            await using (var context = CreateContext())
            {
                var recovery =
                    new LiveTripSessionRecoveryService(
                        new EfLiveTripSessionRepository(context),
                        new TestRecoveryAccessChecker(
                            seed.TripId,
                            seed.OwnerUserId));

                var paused =
                    await recovery.RecoverAsync(
                        sessionId,
                        seed.OwnerUserId);

                Assert.NotNull(paused);

                Assert.Equal(
                    LiveTripStatus.Paused,
                    paused.Status);
            }

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    FinishedAt);

                await service.FinishAsync(sessionId);
            }

            await using (var context = CreateContext())
            {
                var recovery =
                    new LiveTripSessionRecoveryService(
                        new EfLiveTripSessionRepository(context),
                        new TestRecoveryAccessChecker(
                            seed.TripId,
                            seed.OwnerUserId));

                var finished =
                    await recovery.RecoverAsync(
                        sessionId,
                        seed.OwnerUserId);

                Assert.Null(finished);
            }
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 10
    // Unauthorized recovery must fail.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task Recovery_ShouldRejectUnauthorizedUser()
    {
        var seed = await SeedAsync();

        try
        {
            var sessionId =
                await StartSessionAsync(seed.TripId);

            await using var context = CreateContext();

            var recovery =
                new LiveTripSessionRecoveryService(
                    new EfLiveTripSessionRepository(context),
                    new TestRecoveryAccessChecker(
                        seed.TripId,
                        seed.OwnerUserId));

            var outsiderId =
                Guid.NewGuid().ToString("D");

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => recovery.RecoverAsync(
                    sessionId,
                    outsiderId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 11
    // Invalid lifecycle transitions must not corrupt SQL.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task InvalidTransitions_ShouldNotCorruptSqlState()
    {
        var seed = await SeedAsync();

        try
        {
            var sessionId =
                await StartSessionAsync(seed.TripId);

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    StartedAt);

                await service.PauseAsync(sessionId);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PauseAsync(sessionId));
            }

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    StartedAt);

                await service.ResumeAsync(sessionId);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.ResumeAsync(sessionId));
            }

            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    FinishedAt);

                await service.FinishAsync(sessionId);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PauseAsync(sessionId));
            }

            await using var verifyContext = CreateContext();

            var saved =
                await verifyContext.LiveTripSessions
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.SessionId == sessionId);

            Assert.Equal(
                LiveTripStatus.Finished,
                saved.Status);

            Assert.Equal(
                FinishedAt,
                saved.FinishedAtUtc);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // -----------------------------------------------------
    // TEST 12
    // Updating a session that does not exist must fail.
    // -----------------------------------------------------

    [M3LiveSessionSqlFact]
    public async Task UnknownSession_UpdateShouldFail()
    {
        var seed = await SeedAsync();

        try
        {
            await using var context = CreateContext();

            var sessionRepository =
                new EfLiveTripSessionRepository(context);

            var unknown = LiveTripSession.Start(
                Guid.NewGuid(),
                seed.TripId,
                StartedAt);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => sessionRepository.UpdateAsync(unknown));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }
}
