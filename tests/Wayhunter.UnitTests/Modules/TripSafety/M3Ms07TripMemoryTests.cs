using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3Ms07TripMemoryTests
{
    [Fact]
    public async Task OwnerCreatesAndReadsPrivateMemoryLinkedToOwnedStop()
    {
        var f = await Fixture.CreateAsync();
        var stop = f.Trip.AddStop("Station");
        var created = await f.Memories.CreateAsync(f.Trip.Id, f.Owner, Guid.NewGuid(), stop.Id,
            "  A memory  ", TripMemoryVisibility.Private, default);

        Assert.True(created.Created);
        Assert.Equal("A memory", created.Memory.Caption);
        Assert.Equal(stop.Id, created.Memory.TripStopId);
        Assert.Equal(created.Memory, await f.Memories.GetAsync(f.Trip.Id, created.Memory.MemoryId, f.Owner, default));
        Assert.Single(await f.Memories.ListAsync(f.Trip.Id, f.Owner, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivateAndProposedPublicMemoriesAreHiddenFromNonowners(bool isPublic)
    {
        var f = await Fixture.CreateAsync();
        var visibility = isPublic ? TripMemoryVisibility.Public : TripMemoryVisibility.Private;
        var memory = await f.CreateMemoryAsync(visibility);
        var outsider = Guid.NewGuid().ToString("D");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Memories.GetAsync(f.Trip.Id, memory, outsider, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Memories.ListAsync(f.Trip.Id, outsider, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Memories.DeleteAsync(f.Trip.Id, memory, outsider, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Memories.CreateAsync(f.Trip.Id, outsider,
            Guid.NewGuid(), null, null, visibility, default));

        Assert.NotNull(await f.Memories.GetAsync(f.Trip.Id, memory, f.Owner, default));
    }

    [Fact]
    public async Task InvalidTripAndCrossTripStopAreRejected()
    {
        var f = await Fixture.CreateAsync();
        var otherStop = new TripPlan(f.Owner, "Other", f.Trip.StartDate, f.Trip.EndDate, 100m, "Car", 2)
            .AddStop("Other");
        await Assert.ThrowsAsync<ArgumentException>(() => f.Memories.CreateAsync(Guid.Empty, f.Owner,
            Guid.NewGuid(), null, null, TripMemoryVisibility.Private, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Memories.ListAsync(Guid.NewGuid(), f.Owner, default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Memories.CreateAsync(f.Trip.Id, f.Owner,
            Guid.NewGuid(), otherStop.Id, null, TripMemoryVisibility.Private, default));
    }

    [Fact]
    public async Task ConcurrentCreateRetriesReturnOneMemoryAndDeletionHidesIt()
    {
        var f = await Fixture.CreateAsync();
        var id = Guid.NewGuid();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Enumerable.Range(0, 12).Select(async _ =>
        {
            await start.Task;
            return await f.Memories.CreateAsync(f.Trip.Id, f.Owner, id, null, "Caption",
                TripMemoryVisibility.Private, default);
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(pending);
        var memory = Assert.Single(results, x => x.Created).Memory;
        Assert.All(results, result => Assert.Equal(memory, result.Memory));
        await Assert.ThrowsAsync<TripMemoryConflictException>(() => f.Memories.CreateAsync(f.Trip.Id, f.Owner,
            id, null, "Changed", TripMemoryVisibility.Private, default));

        await f.Memories.DeleteAsync(f.Trip.Id, memory.MemoryId, f.Owner, default);
        await f.Memories.DeleteAsync(f.Trip.Id, memory.MemoryId, f.Owner, default);
        Assert.Empty(await f.Memories.ListAsync(f.Trip.Id, f.Owner, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Memories.GetAsync(f.Trip.Id, memory.MemoryId, f.Owner, default));
        await Assert.ThrowsAsync<TripMemoryConflictException>(() => f.Memories.CreateAsync(f.Trip.Id, f.Owner,
            id, null, "Caption", TripMemoryVisibility.Private, default));
    }

    [Theory]
    [InlineData("application/octet-stream", 10)]
    [InlineData("image/png", 0)]
    [InlineData("image/jpeg", 6000000)]
    [InlineData("video/mp4", 20000000)]
    public async Task InvalidMediaIsRejectedBeforeProviderGrant(string type, long size)
    {
        var f = await Fixture.CreateAsync();
        var memory = await f.CreateMemoryAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Media.BeginAsync(f.Trip.Id, memory, f.Owner, Guid.NewGuid(), type, size, default));
        Assert.Equal(0, f.Provider.Grants);
    }

    [Fact]
    public async Task DifferentUserAndDifferentTripCannotReserveOrCompleteUpload()
    {
        var f = await Fixture.CreateAsync();
        var memory = await f.CreateMemoryAsync();
        var operation = Guid.NewGuid();
        var outsider = Guid.NewGuid().ToString("D");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Media.BeginAsync(f.Trip.Id, memory, outsider,
            operation, "image/png", Provider.Png.Length, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Media.CompleteAsync(
            Guid.NewGuid(), memory, f.Owner, operation, default));
        Assert.Equal(0, f.Store.Reservations);
        Assert.Equal(0, f.Provider.Grants);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task VerifiedMetadataDoesNotOverrideTruncatedOrOversizedActualContent(int sizeDelta)
    {
        var f = await Fixture.CreateAsync();
        var memory = await f.CreateMemoryAsync();
        var id = Guid.NewGuid();
        await f.BeginAsync(memory, id);
        f.Provider.Content = new byte[Provider.Png.Length + sizeDelta];

        await Assert.ThrowsAsync<ArgumentException>(() => f.CompleteAsync(memory, id));
        Assert.Equal(0, f.Store.Attachments);
    }

    [Fact]
    public async Task GrantFailureAndCorruptBytesNeverAttachAndSameOperationCanRetry()
    {
        var f = await Fixture.CreateAsync();
        var memory = await f.CreateMemoryAsync();
        var id = Guid.NewGuid();
        f.Provider.Unavailable = true;
        await Assert.ThrowsAsync<TripMemoryUnavailableException>(() => f.BeginAsync(memory, id));
        Assert.Null((await f.Memories.GetAsync(f.Trip.Id, memory, f.Owner, default)).Media);

        f.Provider.Unavailable = false;
        Assert.Equal(id, (await f.BeginAsync(memory, id)).OperationId);
        Assert.Equal(id, (await f.BeginAsync(memory, id)).OperationId);
        await Assert.ThrowsAsync<TripMemoryConflictException>(() => f.BeginAsync(memory, Guid.NewGuid()));
        Assert.Equal(1, f.Store.Reservations);

        f.Provider.Corrupt = true;
        await Assert.ThrowsAsync<ArgumentException>(() => f.CompleteAsync(memory, id));
        Assert.Null((await f.Memories.GetAsync(f.Trip.Id, memory, f.Owner, default)).Media);
        f.Provider.Corrupt = false;
        var attached = await f.CompleteAsync(memory, id);
        Assert.Equal(attached, await f.CompleteAsync(memory, id));
        Assert.Equal(1, f.Store.Attachments);
        Assert.Equal(new TripMemoryMediaMetadata("image/png", Provider.Png.Length), attached);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("operation")]
    [InlineData("public")]
    [InlineData("undecoded")]
    public async Task CompletionRejectsProviderEvidenceOutsideTheOperation(string failure)
    {
        var f = await Fixture.CreateAsync();
        var memory = await f.CreateMemoryAsync();
        var id = Guid.NewGuid();
        await f.BeginAsync(memory, id);
        f.Provider.Alter = asset => failure switch
        {
            "owner" => asset with { OwnerUserId = Guid.NewGuid().ToString("D") },
            "operation" => asset with { OperationId = Guid.NewGuid() },
            "public" => asset with { IsPrivate = false },
            _ => asset with { ContentDecoded = false }
        };

        await Assert.ThrowsAsync<TripMemoryConflictException>(() => f.CompleteAsync(memory, id));
        Assert.Equal(0, f.Store.Attachments);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("shared")]
    [InlineData("cas")]
    public async Task CleanupNeverDeletesUnprovenSharedOrUnclaimedObject(string failure)
    {
        var f = await Fixture.CreateAsync();
        var memory = await f.CreateMemoryAsync();
        var id = Guid.NewGuid();
        await f.BeginAsync(memory, id);
        await f.Memories.DeleteAsync(f.Trip.Id, memory, f.Owner, default);
        f.Store.BlockCleanup = failure == "cas";
        f.Store.SharedReference = failure == "shared";
        if (failure == "owner")
            f.Provider.Alter = asset => asset with { OwnerUserId = Guid.NewGuid().ToString("D") };

        Assert.False(await f.Cleanup.CleanAsync(id, default));
        Assert.Equal(0, f.Provider.Deletes);
    }

    [Fact]
    public async Task CleanupRetriesFrozenClaimAfterProviderFailure()
    {
        var f = await Fixture.CreateAsync();
        var memory = await f.CreateMemoryAsync();
        var id = Guid.NewGuid();
        await f.BeginAsync(memory, id);
        await f.CompleteAsync(memory, id);
        await f.Memories.DeleteAsync(f.Trip.Id, memory, f.Owner, default);
        f.Provider.FailDelete = true;
        await Assert.ThrowsAsync<TripMemoryUnavailableException>(() => f.Cleanup.CleanAsync(id, default));
        Assert.NotNull(await f.Store.ReadCleanupClaimAsync(id, default));

        f.Provider.FailDelete = false;
        Assert.True(await f.Cleanup.CleanAsync(id, default));
        Assert.False(await f.Cleanup.CleanAsync(id, default));
        Assert.Equal(1, f.Provider.Deletes);
    }

    private sealed class Fixture
    {
        public string Owner { get; } = Guid.NewGuid().ToString("D");
        public TripPlan Trip { get; private set; } = null!;
        public Store Store { get; } = new();
        public Provider Provider { get; } = new();
        public TripMemoryService Memories { get; private set; } = null!;
        public TripMemoryMediaService Media { get; private set; } = null!;
        public TripMemoryCleanupService Cleanup { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            f.Trip = new TripPlan(f.Owner, "Trip", date, date.AddDays(1), 100m, "Car", 2);
            var trips = new InMemoryTripPlanRepository();
            await trips.AddAsync(f.Trip);
            f.Memories = new(trips, f.Store, TimeProvider.System);
            f.Media = new(f.Memories, f.Store, f.Provider, TimeProvider.System);
            f.Cleanup = new(f.Store, f.Provider);
            return f;
        }

        public async Task<Guid> CreateMemoryAsync(TripMemoryVisibility visibility = TripMemoryVisibility.Private) =>
            (await Memories.CreateAsync(Trip.Id, Owner, Guid.NewGuid(), null, null, visibility, default)).Memory.MemoryId;

        public Task<MemoryUploadGrant> BeginAsync(Guid memory, Guid operation) =>
            Media.BeginAsync(Trip.Id, memory, Owner, operation, "image/png", Provider.Png.Length, default);
        public Task<TripMemoryMediaMetadata> CompleteAsync(Guid memory, Guid operation) =>
            Media.CompleteAsync(Trip.Id, memory, Owner, operation, default);
    }

    private sealed class Provider : ITripMemoryMediaProvider
    {
        public static readonly byte[] Png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a+wAAAABJRU5ErkJggg==");
        public int Grants { get; private set; }
        public int Deletes { get; private set; }
        public bool Unavailable { get; set; }
        public bool FailDelete { get; set; }
        public bool Corrupt { get; set; }
        public byte[]? Content { get; set; }
        public Func<VerifiedMemoryAsset, VerifiedMemoryAsset> Alter { get; set; } = asset => asset;

        public Task<MemoryUploadGrant> IssueAsync(TripMemoryMedia operation, CancellationToken ct)
        {
            if (Unavailable)
                throw new TripMemoryUnavailableException();

            Grants++;
            return Task.FromResult(new MemoryUploadGrant(operation.OperationId,
                new Uri("https://example.invalid/upload"), operation.CreatedAtUtc.AddMinutes(5)));
        }

        public Task<VerifiedMemoryAsset?> InspectAsync(TripMemoryMedia operation, CancellationToken ct) =>
            Task.FromResult<VerifiedMemoryAsset?>(Alter(new(operation.OperationId, operation.MemoryId,
                operation.OwnerUserId, "test-object", "v1", operation.ContentType, operation.ContentLength, true, true)));

        public Task<Stream> OpenVerifiedContentAsync(VerifiedMemoryAsset asset, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(Content ?? (Corrupt ? new byte[Png.Length] : Png)));

        public Task DeleteVerifiedVersionAsync(VerifiedMemoryAsset asset, CancellationToken ct)
        {
            if (FailDelete)
                throw new TripMemoryUnavailableException();

            Deletes++;
            return Task.CompletedTask;
        }
    }

    private sealed class Store : ITripMemoryStore
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, TripMemory> _memories = [];
        private TripMemoryMedia? _operation;
        private MemoryCleanupClaim? _claim;

        public bool IsAvailable => true;
        public bool BlockCleanup { get; set; }
        public bool SharedReference { get; set; }
        public int Reservations { get; private set; }
        public int Attachments { get; private set; }

        public Task<TripMemoryStoreCreateResult> CreateAsync(TripMemory candidate, CancellationToken ct)
        {
            lock (_sync)
            {
                var existing = _memories.Values.SingleOrDefault(x => x.TripPlanId == candidate.TripPlanId &&
                    x.OwnerUserId == candidate.OwnerUserId && x.CreateOperationId == candidate.CreateOperationId);
                if (existing is not null)
                    return Task.FromResult(new TripMemoryStoreCreateResult(existing, false));

                _memories.Add(candidate.MemoryId, candidate);
                return Task.FromResult(new TripMemoryStoreCreateResult(candidate, true));
            }
        }

        public Task<TripMemory?> FindAsync(Guid tripId, Guid memoryId, CancellationToken ct) =>
            Task.FromResult(_memories.GetValueOrDefault(memoryId));
        public Task<IReadOnlyList<TripMemory>> ListOwnedAsync(Guid tripId, string ownerId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TripMemory>>(_memories.Values.ToArray());
        public Task<TripMemoryMediaMetadata?> ReadAttachedMediaAsync(Guid memoryId, string ownerId, CancellationToken ct) =>
            Task.FromResult<TripMemoryMediaMetadata?>(_operation is { State: TripMemoryMediaState.Attached } item &&
                item.MemoryId == memoryId && item.OwnerUserId == ownerId ? new(item.ContentType, item.ContentLength) : null);

        public Task<bool> SoftDeleteAsync(Guid tripId, Guid memoryId, string ownerId, DateTimeOffset now, CancellationToken ct)
        {
            if (!_memories.TryGetValue(memoryId, out var memory) || memory.TripPlanId != tripId || memory.OwnerUserId != ownerId)
                return Task.FromResult(false);
            _memories[memoryId] = memory with { DeletedAtUtc = memory.DeletedAtUtc ?? now };
            return Task.FromResult(true);
        }

        public Task<TripMemoryMedia> ReserveMediaAsync(TripMemory memory, TripMemoryMedia candidate, CancellationToken ct)
        {
            if (_operation is null)
            {
                _operation = candidate;
                Reservations++;
            }
            return Task.FromResult(_operation);
        }

        public Task<TripMemoryMedia?> FindMediaAsync(Guid memoryId, Guid operationId, string ownerId, CancellationToken ct) =>
            Task.FromResult(_operation);

        public Task AttachAsync(TripMemory memory, TripMemoryMedia operation, VerifiedMemoryAsset asset, CancellationToken ct)
        {
            _operation = operation with { State = TripMemoryMediaState.Attached };
            Attachments++;
            return Task.CompletedTask;
        }

        public Task<MemoryCleanupClaim?> ReadCleanupClaimAsync(Guid operationId, CancellationToken ct) =>
            Task.FromResult(_claim);
        public Task<TripMemoryMedia?> FindCleanupCandidateAsync(Guid operationId, CancellationToken ct) =>
            Task.FromResult(_operation is { State: TripMemoryMediaState.Pending or TripMemoryMediaState.Attached } operation &&
                _memories[operation.MemoryId].DeletedAtUtc is not null ? operation : null);

        public Task<MemoryCleanupClaim?> ClaimCleanupAsync(TripMemoryMedia candidate, VerifiedMemoryAsset asset, CancellationToken ct)
        {
            if (BlockCleanup || SharedReference || _operation != candidate)
                return Task.FromResult<MemoryCleanupClaim?>(null);

            _operation = candidate with { State = TripMemoryMediaState.CleanupClaimed };
            _claim = new(Guid.NewGuid(), _operation, asset);
            return Task.FromResult<MemoryCleanupClaim?>(_claim);
        }

        public Task CompleteCleanupAsync(MemoryCleanupClaim claim, CancellationToken ct)
        {
            Assert.Equal(_claim, claim);
            _operation = claim.Operation with { State = TripMemoryMediaState.Cleaned };
            _claim = null;
            return Task.CompletedTask;
        }
    }
}
