
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementRepositoryPersistenceTests
{
    private static WayhunterSharedDbContext CreateDbContext(
        string databaseName)
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static AdvertisementCreateRequest ValidCreateRequest()
    {
        return new AdvertisementCreateRequest
        {
            Title = "Wayhunter Test Advertisement",
            ImageUrl = "https://example.com/banner.png",
            DestinationUrl = "https://example.com/travel",
            StartAtUtc = new DateTimeOffset(
                2026, 11, 18, 0, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(
                2026, 12, 18, 0, 0, 0, TimeSpan.Zero),
            Placement = AdvertisementPlacement.HomeBanner
        };
    }

    [Fact]
    public async Task CreateAsync_PersistsDraftAdvertisement()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext(databaseName);
        var repository = new AdvertisementRepository(db);

        var created = await repository.CreateAsync(
            ValidCreateRequest());

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(
            AdvertisementStatus.Draft,
            created.Status);
        Assert.False(created.IsEnabled);
        Assert.Equal(
            AdvertisementPlacement.HomeBanner,
            created.Placement);

        await using var verifyDb = CreateDbContext(databaseName);
        var verifyRepository =
            new AdvertisementRepository(verifyDb);

        var persisted = await verifyRepository.GetByIdAsync(
            created.Id);

        Assert.NotNull(persisted);
        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(created.Title, persisted.Title);
        Assert.Equal(created.ImageUrl, persisted.ImageUrl);
    }

    [Fact]
    public async Task UpdateAsync_PersistsAdvertisementDetails()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext(databaseName);
        var repository = new AdvertisementRepository(db);

        var created = await repository.CreateAsync(
            ValidCreateRequest());

        var update = new AdvertisementUpdateRequest
        {
            Title = "Updated Sponsored Advertisement",
            ImageUrl = "https://example.com/updated.png",
            DestinationUrl = "https://example.com/new-page",
            StartAtUtc = new DateTimeOffset(
                2026, 11, 19, 0, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(
                2026, 12, 19, 0, 0, 0, TimeSpan.Zero),
            Placement = AdvertisementPlacement.SponsoredPoi
        };

        var updated = await repository.UpdateAsync(
            created.Id,
            update);

        Assert.NotNull(updated);
        Assert.Equal(update.Title, updated.Title);
        Assert.Equal(
            AdvertisementPlacement.SponsoredPoi,
            updated.Placement);

        await using var verifyDb = CreateDbContext(databaseName);
        var verifyRepository =
            new AdvertisementRepository(verifyDb);

        var persisted = await verifyRepository.GetByIdAsync(
            created.Id);

        Assert.NotNull(persisted);
        Assert.Equal(update.Title, persisted.Title);
        Assert.Equal(update.ImageUrl, persisted.ImageUrl);
        Assert.Equal(update.DestinationUrl, persisted.DestinationUrl);
        Assert.Equal(update.StartAtUtc, persisted.StartAtUtc);
        Assert.Equal(update.EndAtUtc, persisted.EndAtUtc);
    }

    [Fact]
    public async Task ChangeStatusAsync_ActiveAndPausedArePersisted()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext(databaseName);
        var repository = new AdvertisementRepository(db);

        var created = await repository.CreateAsync(
            ValidCreateRequest());

        var activated = await repository.ChangeStatusAsync(
            created.Id,
            new AdvertisementStatusUpdateRequest
            {
                Status = AdvertisementStatus.Active
            });

        Assert.NotNull(activated);
        Assert.True(activated.IsEnabled);
        Assert.Equal(
            AdvertisementStatus.Active,
            activated.Status);

        var paused = await repository.ChangeStatusAsync(
            created.Id,
            new AdvertisementStatusUpdateRequest
            {
                Status = AdvertisementStatus.Paused
            });

        Assert.NotNull(paused);
        Assert.False(paused.IsEnabled);
        Assert.Equal(
            AdvertisementStatus.Paused,
            paused.Status);

        await using var verifyDb = CreateDbContext(databaseName);
        var verifyRepository =
            new AdvertisementRepository(verifyDb);

        var persisted = await verifyRepository.GetByIdAsync(
            created.Id);

        Assert.NotNull(persisted);
        Assert.False(persisted.IsEnabled);
        Assert.Equal(
            AdvertisementStatus.Paused,
            persisted.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ArchivedCannotBeReactivated()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext(databaseName);
        var repository = new AdvertisementRepository(db);

        var created = await repository.CreateAsync(
            ValidCreateRequest());

        var archived = await repository.ChangeStatusAsync(
            created.Id,
            new AdvertisementStatusUpdateRequest
            {
                Status = AdvertisementStatus.Archived
            });

        Assert.NotNull(archived);
        Assert.Equal(
            AdvertisementStatus.Archived,
            archived.Status);
        Assert.False(archived.IsEnabled);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ChangeStatusAsync(
                created.Id,
                new AdvertisementStatusUpdateRequest
                {
                    Status = AdvertisementStatus.Active
                }));

        await using var verifyDb = CreateDbContext(databaseName);
        var verifyRepository =
            new AdvertisementRepository(verifyDb);

        var persisted = await verifyRepository.GetByIdAsync(
            created.Id);

        Assert.NotNull(persisted);
        Assert.Equal(
            AdvertisementStatus.Archived,
            persisted.Status);
        Assert.False(persisted.IsEnabled);
    }

    [Fact]
    public async Task InvalidCreateRequest_DoesNotPersist()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext(databaseName);
        var repository = new AdvertisementRepository(db);

        var invalidRequest = ValidCreateRequest() with
        {
            Title = "",
            Placement = AdvertisementPlacement.Unspecified
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.CreateAsync(invalidRequest));

        await using var verifyDb = CreateDbContext(databaseName);

        Assert.Equal(
            0,
            await verifyDb.Advertisements.CountAsync());
    }

    [Fact]
    public async Task MissingAdvertisement_UpdateReturnsNull()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext(databaseName);
        var repository = new AdvertisementRepository(db);

        var request = new AdvertisementUpdateRequest
        {
            Title = "Missing Advertisement",
            ImageUrl = "https://example.com/banner.png",
            DestinationUrl = null,
            StartAtUtc = new DateTimeOffset(
                2026, 11, 18, 0, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(
                2026, 12, 18, 0, 0, 0, TimeSpan.Zero),
            Placement = AdvertisementPlacement.HomeBanner
        };

        var result = await repository.UpdateAsync(
            Guid.NewGuid(),
            request);

        Assert.Null(result);
    }

    [Fact]
    public async Task MissingAdvertisement_StatusChangeReturnsNull()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext(databaseName);
        var repository = new AdvertisementRepository(db);

        var result = await repository.ChangeStatusAsync(
            Guid.NewGuid(),
            new AdvertisementStatusUpdateRequest
            {
                Status = AdvertisementStatus.Active
            });

        Assert.Null(result);
    }
}
