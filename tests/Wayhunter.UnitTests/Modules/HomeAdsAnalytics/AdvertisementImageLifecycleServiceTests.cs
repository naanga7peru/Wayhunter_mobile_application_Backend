using Wayhunter.Api.Services.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageLifecycleServiceTests
{
    private static readonly AdvertisementValidatedImage Image =
        new([1, 2, 3], "image/png", ".png");

    [Fact]
    public async Task StorageFailure_DoesNotUpdateDatabase()
    {
        var storage = new FakeStorage { FailUpload = true };
        var repository = new FakeUpdateRepository();

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }

    [Fact]
    public async Task ConcurrentUpdateConflict_CleansNewAsset()
    {
        var storage = new FakeStorage();
        var repository = new FakeUpdateRepository();

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var result = await service.ReplaceAsync(
            Guid.NewGuid(),
            "https://example.com/old.png",
            Image);

        Assert.Null(result);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(1, storage.DeleteCalls);
        Assert.Equal("m7/test-asset", storage.LastDeletedAssetId);
    }

    [Fact]
    public async Task DatabaseFailure_PreservesAssetForReconciliation()
    {
        var storage = new FakeStorage();
        var repository = new FakeUpdateRepository
        {
            FailUpdate = true
        };

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Contains("Fake DB failure", error.Message);
        Assert.Equal(1, storage.UploadCalls);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }

    [Fact]
    public async Task ConflictCleanupFailure_IsNotSilentlyIgnored()
    {
        var storage = new FakeStorage
        {
            DeleteReturnsFalse = true
        };

        var repository = new FakeUpdateRepository();

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Contains(
            "cleanup was not confirmed",
            exception.Message);

        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(1, storage.DeleteCalls);
        Assert.Equal("m7/test-asset", storage.LastDeletedAssetId);
    }
    [Fact]
    public async Task DatabaseFailure_DoesNotAttemptUnsafeCleanup()
    {
        var storage = new FakeStorage
        {
            DeleteReturnsFalse = true
        };

        var repository = new FakeUpdateRepository
        {
            FailUpdate = true
        };

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Contains("Fake DB failure", error.Message);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }
    [Fact]
    public async Task SuccessfulReplacement_KeepsUploadedAsset()
    {
        var storage = new FakeStorage();

        var repository = new FakeUpdateRepository
        {
            ReturnUpdatedDto = true
        };

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var advertisementId = Guid.NewGuid();

        var result = await service.ReplaceAsync(
            advertisementId,
            "https://example.com/old.png",
            Image);

        Assert.NotNull(result);
        Assert.Equal(advertisementId, result.Id);
        Assert.Equal(
            "https://example.com/new.png",
            result.ImageUrl);

        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);

        Assert.Equal(
            "https://example.com/old.png",
            repository.LastExpectedImageUrl);

        Assert.Equal(
            "https://example.com/new.png",
            repository.LastTrustedImageUrl);
    }
    [Fact]
    public async Task InvalidStorageUrl_CleansAssetWithoutDatabaseUpdate()
    {
        var storage = new FakeStorage
        {
            UploadedUrl = "http://example.com/insecure.png"
        };

        var repository = new FakeUpdateRepository();

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Contains(
            "invalid HTTPS URL",
            exception.Message);

        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal(1, storage.DeleteCalls);
        Assert.Equal("m7/test-asset", storage.LastDeletedAssetId);
    }
    [Fact]
    public async Task EmptyStorageAssetId_PreventsDatabaseUpdate()
    {
        var storage = new FakeStorage
        {
            UploadedAssetId = ""
        };

        var repository = new FakeUpdateRepository();

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Equal(2, exception.InnerExceptions.Count);

        Assert.Contains(
            "invalid asset identifier",
            exception.InnerExceptions[0].Message);

        Assert.Contains(
            "could not identify",
            exception.InnerExceptions[1].Message);

        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }
    [Fact]
    public async Task TwoConcurrentAdmins_OnlyWinnerAssetIsRetained()
    {
        var advertisementId = Guid.NewGuid();
        const string oldUrl = "https://example.com/old.png";
        const string firstUrl = "https://example.com/first.png";
        const string secondUrl = "https://example.com/second.png";

        var firstStorage = new FakeStorage
        {
            UploadedAssetId = "m7/first-asset",
            UploadedUrl = firstUrl
        };

        var secondStorage = new FakeStorage
        {
            UploadedAssetId = "m7/second-asset",
            UploadedUrl = secondUrl
        };

        var repository = new ConcurrentCasRepository(oldUrl);

        var firstService = new AdvertisementImageLifecycleService(
            firstStorage, repository);

        var secondService = new AdvertisementImageLifecycleService(
            secondStorage, repository);

        var firstTask = firstService.ReplaceAsync(
            advertisementId, oldUrl, Image);

        var secondTask = secondService.ReplaceAsync(
            advertisementId, oldUrl, Image);

        var results = await Task.WhenAll(
            firstTask, secondTask);

        // Exactly one conditional update wins.
        Assert.Single(results, result => result is not null);
        Assert.Single(results, result => result is null);

        Assert.Equal(2, repository.UpdateCalls);

        var firstWon = results[0] is not null;

        Assert.Equal(
            firstWon ? firstUrl : secondUrl,
            repository.CurrentImageUrl);

        Assert.Equal(
            firstWon ? 0 : 1,
            firstStorage.DeleteCalls);

        Assert.Equal(
            firstWon ? 1 : 0,
            secondStorage.DeleteCalls);

        // Cleanup must target only the losing request.
        Assert.Equal(
            firstWon ? "m7/second-asset" : "m7/first-asset",
            firstWon
                ? secondStorage.LastDeletedAssetId
                : firstStorage.LastDeletedAssetId);

        Assert.Null(
            firstWon
                ? firstStorage.LastDeletedAssetId
                : secondStorage.LastDeletedAssetId);
    }

    private sealed class ConcurrentCasRepository
        : IAdvertisementImageUpdateRepository
    {
        private readonly object _sync = new();
        private readonly TaskCompletionSource<bool> _bothArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _arrivals;

        public ConcurrentCasRepository(string initialImageUrl)
        {
            CurrentImageUrl = initialImageUrl;
        }

        public string CurrentImageUrl { get; private set; }

        public int UpdateCalls => Volatile.Read(ref _arrivals);

        public async Task<AdvertisementDto?> UpdateImageUrlAsync(
            Guid advertisementId,
            string expectedImageUrl,
            string trustedHttpsUrl,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _bothArrived.TrySetResult(true);
            }

            // Ensure both Admin requests reach the CAS stage.
            await _bothArrived.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            lock (_sync)
            {
                if (CurrentImageUrl != expectedImageUrl)
                {
                    return null;
                }

                CurrentImageUrl = trustedHttpsUrl;

                return new AdvertisementDto
                {
                    Id = advertisementId,
                    ImageUrl = trustedHttpsUrl
                };
            }
        }
    }
    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("http://example.com/old.png")]
    public async Task InvalidExistingImageUrl_IsRejectedBeforeStorageUpload(
        string existingImageUrl)
    {
        var storage = new FakeStorage();
        var repository = new FakeUpdateRepository();

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                existingImageUrl,
                Image));

        Assert.Equal(0, storage.UploadCalls);
        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }
    [Fact]
    public async Task DatabaseFailure_DoesNotInvokeThrowingCleanup()
    {
        var storage = new FakeStorage
        {
            DeleteThrows = true
        };

        var repository = new FakeUpdateRepository
        {
            FailUpdate = true
        };

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Contains("Fake DB failure", error.Message);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }
    [Fact]
    public async Task CancelledBeforeUpload_DoesNotCallStorage()
    {
        var storage = new FakeStorage();
        var repository = new FakeUpdateRepository();

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image,
                cts.Token));

        Assert.Equal(0, storage.UploadCalls);
        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }

    [Fact]
    public async Task CancelledDuringDatabaseUpdate_PreservesAsset()
    {
        var storage = new FakeStorage();

        using var cts = new CancellationTokenSource();

        var repository = new FakeUpdateRepository
        {
            CancelDuringUpdate = cts
        };

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image,
                cts.Token));

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(1, storage.UploadCalls);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(0, storage.DeleteCalls);
    }
    [Fact]
    public async Task CommittedUpdateButResponseLost_DoesNotDeleteNewImage()
    {
        var storage = new FakeStorage();

        var repository = new FakeUpdateRepository
        {
            SimulateCommitThenThrow = true
        };

        var service = new AdvertisementImageLifecycleService(
            storage, repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceAsync(
                Guid.NewGuid(),
                "https://example.com/old.png",
                Image));

        Assert.Contains("response lost", error.Message);

        Assert.Equal(
            "https://example.com/new.png",
            repository.LastCommittedImageUrl);

        Assert.Equal(1, storage.UploadCalls);
        Assert.Equal(1, repository.UpdateCalls);

        // Asset must survive the ambiguous SQL outcome.
        Assert.Equal(0, storage.DeleteCalls);
    }
    private sealed class FakeStorage : IAdvertisementImageStorage
    {
        public bool FailUpload { get; set; }
        public int UploadCalls { get; private set; }
        public string UploadedAssetId { get; set; } =
            "m7/test-asset";
        public string UploadedUrl { get; set; } =
            "https://example.com/new.png";
        public bool DeleteReturnsFalse { get; set; }
        public bool DeleteThrows { get; set; }
        public int DeleteCalls { get; private set; }
        public bool? LastDeleteTokenWasCancelled { get; private set; }
        public string? LastDeletedAssetId { get; private set; }

        public Task<AdvertisementStoredImage> UploadAsync(
        Guid advertisementId,
        AdvertisementValidatedImage image,
            CancellationToken cancellationToken = default)
        {
            if (advertisementId == Guid.Empty)
                throw new ArgumentException("Invalid advertisement ID.", nameof(advertisementId));
            UploadCalls++;

            if (FailUpload)
                throw new InvalidOperationException("Fake upload failure.");

            return Task.FromResult(
                new AdvertisementStoredImage(
                    UploadedAssetId,
                    UploadedUrl));
        }

        public Task<bool> TryDeleteOwnedAssetAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            LastDeletedAssetId = assetId;
            LastDeleteTokenWasCancelled = cancellationToken.IsCancellationRequested;

            if (DeleteThrows)
                throw new InvalidOperationException(
                    "Fake provider cleanup failure.");

            return Task.FromResult(!DeleteReturnsFalse);
        }
    }

    private sealed class FakeUpdateRepository
        : IAdvertisementImageUpdateRepository
    {
        public bool FailUpdate { get; set; }
        public bool SimulateCommitThenThrow { get; set; }
        public string? LastCommittedImageUrl { get; private set; }
        public CancellationTokenSource? CancelDuringUpdate { get; set; }
        public bool ReturnUpdatedDto { get; set; }
        public string? LastExpectedImageUrl { get; private set; }
        public string? LastTrustedImageUrl { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task<AdvertisementDto?> UpdateImageUrlAsync(
            Guid advertisementId,
            string expectedImageUrl,
            string trustedHttpsUrl,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;

            LastExpectedImageUrl = expectedImageUrl;
            LastTrustedImageUrl = trustedHttpsUrl;

            if (CancelDuringUpdate is not null)
            {
                CancelDuringUpdate.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (SimulateCommitThenThrow)
            {
                LastCommittedImageUrl = trustedHttpsUrl;

                throw new InvalidOperationException(
                    "Fake SQL commit succeeded but response lost.");
            }

            if (FailUpdate)
                throw new InvalidOperationException("Fake DB failure.");

            if (ReturnUpdatedDto)
            {
                return Task.FromResult<AdvertisementDto?>(
                    new AdvertisementDto
                    {
                        Id = advertisementId,
                        ImageUrl = trustedHttpsUrl
                    });
            }

            // Null simulates a compare-and-swap conflict.
            return Task.FromResult<AdvertisementDto?>(null);
        }
    }
}
