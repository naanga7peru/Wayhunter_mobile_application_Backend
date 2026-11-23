using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wayhunter.Api.Extensions;

public static class TripMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddTripMemoryServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddScoped<TripMemoryService>();
        services.AddScoped<TripMemoryMediaService>();
        services.TryAddSingleton(TimeProvider.System);
        if (configuration?.GetValue<bool>("TripSafety:EnableSqlTripMemories") == true)
        {
            // The host's shared SQL context is already configured by the independently
            // gated TripSafety persistence registration. This flag only swaps the
            // memory store; it never runs migrations or enables media storage.
            services.AddWayhunterSharedPersistence(configuration);
            services.RemoveAll<ITripMemoryStore>();
            services.AddScoped<ITripMemoryStore, EfTripMemoryStore>();
        }
        else
        {
            services.TryAddSingleton<ITripMemoryStore, UnavailableTripMemoryDependencies>();
        }
        services.TryAddSingleton<ITripMemoryMediaProvider, UnavailableTripMemoryDependencies>();
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
