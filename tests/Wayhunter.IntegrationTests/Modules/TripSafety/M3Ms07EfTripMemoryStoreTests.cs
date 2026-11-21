using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3Ms07EfTripMemoryStoreTests
{
    [Fact]
    public async Task ExistingStoreContractPreservesOwnershipIdempotencyAndFencedCleanupAcrossContexts()
    {
        var factory = new Factory();
        var now = DateTimeOffset.UtcNow;
        var owner = Guid.NewGuid().ToString("D");
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        var trip = new TripPlan(owner, "Memory test", date, date.AddDays(1), 100, "Car", 2);
        var stop = trip.AddStop("Stop");
        await using (var db = factory.CreateDbContext())
        {
            db.Trips.Add(trip);
            await db.SaveChangesAsync();
        }

        var store = new EfTripMemoryStore(factory, TimeProvider.System);
        var memory = new TripMemory(Guid.NewGuid(), trip.Id, stop.Id, owner, Guid.NewGuid(), "Caption",
            TripMemoryVisibility.Private, now, now);
        Assert.True((await store.CreateAsync(memory, default)).Created);
        Assert.Equal(new TripMemoryStoreCreateResult(memory, false),
            await store.CreateAsync(memory with { MemoryId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<TripMemoryConflictException>(() => store.CreateAsync(memory with { Caption = "Different" }, default));
        await Assert.ThrowsAsync<TripMemoryConflictException>(() => store.CreateAsync(memory with
        {
            CreateOperationId = Guid.NewGuid(), TripStopId = Guid.NewGuid()
        }, default));
        Assert.Empty(await store.ListOwnedAsync(trip.Id, Guid.NewGuid().ToString("D"), default));
        Assert.Null(await store.FindAsync(Guid.NewGuid(), memory.MemoryId, default));

        var upload = new TripMemoryMedia(Guid.NewGuid(), memory.MemoryId, owner, "image/jpeg", 10, now, TripMemoryMediaState.Pending);
        Assert.Equal(upload, await store.ReserveMediaAsync(memory, upload, default));
        Assert.Equal(upload, await store.ReserveMediaAsync(memory, upload with { CreatedAtUtc = now.AddMinutes(1) }, default));
        var asset = new VerifiedMemoryAsset(upload.OperationId, memory.MemoryId, owner, "private/test", "one",
            upload.ContentType, upload.ContentLength, true, true);
        await Assert.ThrowsAsync<TripMemoryConflictException>(() => store.AttachAsync(memory, upload, asset with { IsPrivate = false }, default));
        await store.AttachAsync(memory, upload, asset, default);
        await store.AttachAsync(memory, upload, asset, default);
        Assert.Null(await store.FindCleanupCandidateAsync(upload.OperationId, default));

        Assert.False(await store.SoftDeleteAsync(trip.Id, memory.MemoryId, Guid.NewGuid().ToString("D"), now, default));
        Assert.True(await store.SoftDeleteAsync(trip.Id, memory.MemoryId, owner, now, default));
        Assert.True(await store.SoftDeleteAsync(trip.Id, memory.MemoryId, owner, now.AddMinutes(1), default));
        Assert.Null(await store.FindAsync(trip.Id, memory.MemoryId, default));
        Assert.Null(await store.ReadAttachedMediaAsync(memory.MemoryId, owner, default));
        await Assert.ThrowsAsync<TripMemoryConflictException>(() => store.CreateAsync(memory, default));
        var candidate = await store.FindCleanupCandidateAsync(upload.OperationId, default);
        Assert.NotNull(candidate);
        Assert.Null(await store.ClaimCleanupAsync(candidate, asset with { ObjectVersion = "wrong" }, default));
        var claim = await store.ClaimCleanupAsync(candidate, asset, default);
        Assert.NotNull(claim);
        Assert.Equal(claim.ClaimId, (await store.ReadCleanupClaimAsync(upload.OperationId, default))!.ClaimId);
        await Assert.ThrowsAsync<TripMemoryConflictException>(() =>
            store.CompleteCleanupAsync(claim with { ClaimId = Guid.NewGuid() }, default));
        await store.CompleteCleanupAsync(claim, default);
        await store.CompleteCleanupAsync(claim, default);
        Assert.Null(await store.ReadCleanupClaimAsync(upload.OperationId, default));
    }

    // Verifies the repository state machine. Relational locks/FKs require separately approved SQL.
    private sealed class Factory : IDbContextFactory<WayhunterSharedDbContext>
    {
        private readonly DbContextOptions<WayhunterSharedDbContext> _options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

        public WayhunterSharedDbContext CreateDbContext() => new(_options);
    }
}
