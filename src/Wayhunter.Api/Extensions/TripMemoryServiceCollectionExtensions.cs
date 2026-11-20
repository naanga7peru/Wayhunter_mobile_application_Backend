using Wayhunter.Application.Modules.TripSafety.Memories;

namespace Wayhunter.Api.Extensions;

public static class TripMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddTripMemoryServices(this IServiceCollection services)
    {
        services.AddScoped<TripMemoryService>();
        services.AddScoped<TripMemoryMediaService>();
        services.AddSingleton<ITripMemoryStore, UnavailableTripMemoryDependencies>();
        services.AddSingleton<ITripMemoryMediaProvider, UnavailableTripMemoryDependencies>();
        return services;
    }

    private sealed class UnavailableTripMemoryDependencies : ITripMemoryStore, ITripMemoryMediaProvider
    {
        public bool IsAvailable => false;
        public Task<TripMemoryStoreCreateResult> CreateAsync(TripMemory candidate, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<TripMemory?> FindAsync(Guid tripId, Guid memoryId, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<IReadOnlyList<TripMemory>> ListOwnedAsync(Guid tripId, string ownerId, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<TripMemoryMediaMetadata?> ReadAttachedMediaAsync(Guid memoryId, string ownerId, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<bool> SoftDeleteAsync(Guid tripId, Guid memoryId, string ownerId, DateTimeOffset now, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<TripMemoryMedia> ReserveMediaAsync(TripMemory memory, TripMemoryMedia candidate, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<TripMemoryMedia?> FindMediaAsync(Guid memoryId, Guid operationId, string ownerId, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task AttachAsync(TripMemory memory, TripMemoryMedia operation, VerifiedMemoryAsset asset, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<MemoryCleanupClaim?> ReadCleanupClaimAsync(Guid operationId, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<TripMemoryMedia?> FindCleanupCandidateAsync(Guid operationId, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<MemoryCleanupClaim?> ClaimCleanupAsync(TripMemoryMedia candidate, VerifiedMemoryAsset asset, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task CompleteCleanupAsync(MemoryCleanupClaim claim, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<MemoryUploadGrant> IssueAsync(TripMemoryMedia operation, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<VerifiedMemoryAsset?> InspectAsync(TripMemoryMedia operation, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task<Stream> OpenVerifiedContentAsync(VerifiedMemoryAsset asset, CancellationToken ct) => throw new TripMemoryUnavailableException();
        public Task DeleteVerifiedVersionAsync(VerifiedMemoryAsset asset, CancellationToken ct) => throw new TripMemoryUnavailableException();
    }
}
