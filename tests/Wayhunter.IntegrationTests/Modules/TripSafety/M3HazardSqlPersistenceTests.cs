using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3HazardSqlFactAttribute : FactAttribute
{
    public M3HazardSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M3_TEST_SQL_CONNECTION")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M3_TEST_DATABASE")))
            Skip = "Set the explicitly authorized disposable M3 SQL target to run hazard persistence tests.";
    }
}

public sealed class M3HazardSqlPersistenceTests
{
    [M3HazardSqlFact]
    public async Task TripMemorySqlStore_EnforcesOwnerAndRetainsTombstone()
    {
        var catalog = M3LocalDbProfile.GetAuthorizedDisposableCatalog();
        var connection = M3LocalDbProfile.Validate(
            Environment.GetEnvironmentVariable("WAYHUNTER_M3_TEST_SQL_CONNECTION"), catalog).ConnectionString;
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseSqlServer(connection).Options;
        var factory = new SqlContextFactory(options);
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.RegisterPending("M3 memory SQL owner",
            $"memory-{Guid.NewGuid():N}@example.invalid", "test-only-password-hash", now.AddMinutes(-2));
        user.ActivateAfterVerifiedOtp(now.AddMinutes(-1));
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var trip = new TripPlan(user.UserId, "Memory SQL test", day, day.AddDays(1), 100, "Car", 2);
        var stop = trip.AddStop("Memory SQL stop");
        var memoryId = Guid.NewGuid();
        try
        {
            await using (var db = factory.CreateDbContext())
            {
                db.Users.Add(user);
                db.Trips.Add(trip);
                await db.SaveChangesAsync();
            }

            var store = new EfTripMemoryStore(factory, TimeProvider.System);
            var memory = new TripMemory(memoryId, trip.Id, stop.Id, user.UserId, Guid.NewGuid(),
                "SQL persisted memory", TripMemoryVisibility.Private, now, now);
            Assert.True((await store.CreateAsync(memory, default)).Created);
            Assert.Equal(memory, await store.FindAsync(trip.Id, memoryId, default));
            Assert.Empty(await store.ListOwnedAsync(trip.Id, Guid.NewGuid().ToString("D"), default));
            Assert.Single(await store.ListOwnedAsync(trip.Id, user.UserId, default));
            Assert.False(await store.SoftDeleteAsync(trip.Id, memoryId, Guid.NewGuid().ToString("D"), now, default));
            Assert.True(await store.SoftDeleteAsync(trip.Id, memoryId, user.UserId, now, default));
            Assert.Null(await store.FindAsync(trip.Id, memoryId, default));
            await Assert.ThrowsAsync<TripMemoryConflictException>(() => store.CreateAsync(memory, default));
        }
        finally
        {
            await using var cleanup = new WayhunterSharedDbContext(options);
            await cleanup.TripMemoryMedia.Where(x => x.MemoryId == memoryId).ExecuteDeleteAsync();
            await cleanup.TripMemories.Where(x => x.MemoryId == memoryId).ExecuteDeleteAsync();
            await cleanup.TripStops.Where(x => x.TripPlanId == trip.Id).ExecuteDeleteAsync();
            await cleanup.Trips.Where(x => x.Id == trip.Id).ExecuteDeleteAsync();
            await cleanup.Users.Where(x => x.UserId == user.UserId).ExecuteDeleteAsync();
        }
    }

    [M3HazardSqlFact]
    public async Task HazardPersistence_ActiveExpiryAndConcurrentReviewUseSqlState()
    {
        var catalog = M3LocalDbProfile.GetAuthorizedDisposableCatalog();
        var connection = M3LocalDbProfile.Validate(
            Environment.GetEnvironmentVariable("WAYHUNTER_M3_TEST_SQL_CONNECTION"), catalog).ConnectionString;
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(connection).Options;
        var now = DateTimeOffset.UtcNow;
        var reporter = UserAccount.RegisterPending("M3 hazard SQL reporter",
            $"hazard-{Guid.NewGuid():N}@example.invalid", "test-only-password-hash", now.AddMinutes(-2));
        reporter.ActivateAfterVerifiedOtp(now.AddMinutes(-1));
        var reviewer = UserAccount.RegisterPending("M3 hazard SQL reviewer",
            $"hazard-reviewer-{Guid.NewGuid():N}@example.invalid", "test-only-password-hash", now.AddMinutes(-2));
        reviewer.ActivateAfterVerifiedOtp(now.AddMinutes(-1));

        var activeId = Guid.NewGuid();
        var dueId = Guid.NewGuid();
        try
        {
            await using (var seed = new WayhunterSharedDbContext(options))
            {
                seed.Users.AddRange(reporter, reviewer);
                seed.HazardReports.Add(HazardReport.ReportWithEvidence(activeId, reporter.UserId,
                    now, now.AddMinutes(-2), 6.9271, 79.8612, "SQL persistence test", "opaque-test-asset"));
                seed.HazardReports.Add(HazardReport.Report(dueId, reporter.UserId,
                    now.AddHours(-25), now.AddHours(-26)));
                await seed.SaveChangesAsync();
            }

            await using (var read = new WayhunterSharedDbContext(options))
            {
                var repository = new EfHazardReportRepository(read);
                Assert.Contains(await repository.GetActiveAtAsync(now), x => x.Id == activeId);
                Assert.Contains(await repository.GetDueForExpiryAsync(now, 20), x => x.Id == dueId);
            }

            await using var first = new WayhunterSharedDbContext(options);
            await using var second = new WayhunterSharedDbContext(options);
            var firstRepository = new EfHazardReportRepository(first);
            var secondRepository = new EfHazardReportRepository(second);
            var left = await firstRepository.GetByIdAsync(activeId);
            var right = await secondRepository.GetByIdAsync(activeId);
            Assert.NotNull(left);
            Assert.NotNull(right);
            left.Verify(reviewer.UserId, now.AddMinutes(1));
            right.Resolve(reviewer.UserId, "Concurrent review test", now.AddMinutes(1));
            await secondRepository.UpdateAsync(right);
            await Assert.ThrowsAsync<InvalidOperationException>(() => firstRepository.UpdateAsync(left));

            await using var confirmed = new WayhunterSharedDbContext(options);
            Assert.Equal(HazardStatus.Resolved,
                (await confirmed.HazardReports.SingleAsync(x => x.Id == activeId)).Status);
        }
        finally
        {
            await using var cleanup = new WayhunterSharedDbContext(options);
            await cleanup.HazardReports.Where(x => x.Id == activeId || x.Id == dueId).ExecuteDeleteAsync();
            await cleanup.Users.Where(x => x.UserId == reporter.UserId || x.UserId == reviewer.UserId).ExecuteDeleteAsync();
        }
    }

    private sealed class SqlContextFactory(DbContextOptions<WayhunterSharedDbContext> options)
        : IDbContextFactory<WayhunterSharedDbContext>
    {
        public WayhunterSharedDbContext CreateDbContext() => new(options);
    }
}
