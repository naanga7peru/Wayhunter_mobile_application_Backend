using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageUploadPreparationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulPreparation_PersistsPendingOperation()
    {
        var repository = new FakeRepository();
        var service = new AdvertisementImageUploadPreparationService(
            repository);

        var operationId = Guid.NewGuid();
        var advertisementId = Guid.NewGuid();

        var result = await service.PrepareAsync(
            operationId,
            advertisementId,
            "trusted-m1-admin",
            "https://example.com/old.png",
            Now);

        Assert.Equal(1, repository.CreateCalls);
        Assert.Equal(operationId, result.UploadOperationId);
        Assert.Equal(advertisementId, result.AdvertisementId);
        Assert.Equal("trusted-m1-admin", result.AdministratorId);

        Assert.Equal(
            $"wayhunter/advertisements/{advertisementId:D}/{operationId:N}",
            result.ProviderAssetId);

        Assert.Equal(
            AdvertisementImageRecoveryState.PendingUpload,
            result.State);

        Assert.Equal(0, result.RetryCount);
        Assert.Equal(0, result.Revision);
    }

    [Fact]
    public async Task DuplicateOperation_DoesNotCreateAnotherRecord()
    {
        var repository = new FakeRepository();
        var service = new AdvertisementImageUploadPreparationService(
            repository);

        var operationId = Guid.NewGuid();
        var advertisementId = Guid.NewGuid();

        await service.PrepareAsync(
            operationId,
            advertisementId,
            "trusted-admin",
            "https://example.com/old.png",
            Now);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(
                operationId,
                advertisementId,
                "trusted-admin",
                "https://example.com/old.png",
                Now));

        Assert.Equal(2, repository.CreateCalls);
        Assert.Single(repository.Operations);
    }

    [Fact]
    public async Task StorageUnavailablePreparationFailsClosed()
    {
        var repository = new FakeRepository
        {
            RejectCreate = true
        };

        var service = new AdvertisementImageUploadPreparationService(
            repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "trusted-admin",
                "https://example.com/old.png",
                Now));

        Assert.Empty(repository.Operations);
    }

    [Fact]
    public async Task MissingTrustedAdmin_DoesNotWrite()
    {
        var repository = new FakeRepository();
        var service = new AdvertisementImageUploadPreparationService(
            repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PrepareAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "",
                "https://example.com/old.png",
                Now));

        Assert.Equal(0, repository.CreateCalls);
    }

    [Fact]
    public async Task InvalidPreviousUrl_DoesNotWrite()
    {
        var repository = new FakeRepository();
        var service = new AdvertisementImageUploadPreparationService(
            repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PrepareAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "trusted-admin",
                "http://example.com/old.png",
                Now));

        Assert.Equal(0, repository.CreateCalls);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotWrite()
    {
        var repository = new FakeRepository();
        var service = new AdvertisementImageUploadPreparationService(
            repository);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.PrepareAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "trusted-admin",
                "https://example.com/old.png",
                Now,
                cts.Token));

        Assert.Equal(0, repository.CreateCalls);
    }

    private sealed class FakeRepository
        : IAdvertisementImageRecoveryRepository
    {
        public Dictionary<Guid, AdvertisementImageUploadOperation>
            Operations { get; } = new();

        public bool RejectCreate { get; set; }
        public int CreateCalls { get; private set; }

        public Task<bool> TryCreateAsync(
            AdvertisementImageUploadOperation operation,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;

            if (RejectCreate)
                return Task.FromResult(false);

            return Task.FromResult(
                Operations.TryAdd(
                    operation.UploadOperationId,
                    operation));
        }

        public Task<AdvertisementImageUploadOperation?> GetAsync(
            Guid uploadOperationId,
            CancellationToken cancellationToken = default)
        {
            Operations.TryGetValue(uploadOperationId, out var item);
            return Task.FromResult(item);
        }

        public Task<AdvertisementImageUploadOperation?> TryClaimAsync(
            Guid uploadOperationId,
            Guid claimToken,
            DateTimeOffset nowUtc,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdvertisementImageUploadOperation?>(null);

        public Task<bool> TrySaveAsync(
            AdvertisementImageUploadOperation operation,
            long expectedRevision,
            Guid expectedClaimToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}