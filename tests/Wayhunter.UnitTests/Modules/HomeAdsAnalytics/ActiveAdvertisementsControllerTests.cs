
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Controllers.Mobile.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class ActiveAdvertisementsControllerTests
{
    private sealed class FakeAdvertisementRepository
        : IAdvertisementRepository
    {
        public IReadOnlyList<AdvertisementDto> Advertisements
        { get; set; } = [];

        public Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
            DateTimeOffset nowUtc,
            AdvertisementPlacement? placement = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AdvertisementDto> active = Advertisements
                .Where(ad =>
                    ad.IsEnabled &&
                    ad.Status == AdvertisementStatus.Active &&
                    ad.StartAtUtc <= nowUtc &&
                    nowUtc < ad.EndAtUtc &&
                    (!placement.HasValue || ad.Placement == placement.Value))
                .ToArray();
            return Task.FromResult(active);
        }

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Advertisements);
        }

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Advertisements.FirstOrDefault(x => x.Id == id));
        }
    }

    private static ActiveAdvertisementsController CreateController(
        params AdvertisementDto[] advertisements)
    {
        var repository = new FakeAdvertisementRepository
        {
            Advertisements = advertisements
        };

        var controller = new ActiveAdvertisementsController(
            new AdvertisementService(repository),
            TimeProvider.System);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static AdvertisementDto CreateActiveAd(
        AdvertisementPlacement placement =
            AdvertisementPlacement.HomeBanner)
    {
        var now = DateTimeOffset.UtcNow;

        return new AdvertisementDto
        {
            Id = Guid.NewGuid(),
            Title = "Test Advertisement",
            ImageUrl = "https://example.com/ad.jpg",
            StartAtUtc = now.AddHours(-1),
            EndAtUtc = now.AddHours(1),
            Placement = placement,
            Status = AdvertisementStatus.Active,
            IsEnabled = true
        };
    }

    [Fact]
    public async Task NoPlacement_ReturnsActiveAdvertisements()
    {
        var advertisement = CreateActiveAd();
        var controller = CreateController(advertisement);

        var response = await controller.GetActive(
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var result = Assert.IsAssignableFrom<
            IReadOnlyList<AdvertisementDto>>(ok.Value);

        Assert.Single(result);
    }

    [Fact]
    public async Task Placement_ReturnsMatchingAdvertisement()
    {
        var home = CreateActiveAd(
            AdvertisementPlacement.HomeBanner);

        var poi = CreateActiveAd(
            AdvertisementPlacement.SponsoredPoi);

        var controller = CreateController(home, poi);

        controller.Request.QueryString =
            new QueryString("?placement=HomeBanner");

        var response = await controller.GetActive(
            "HomeBanner",
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var result = Assert.IsAssignableFrom<
            IReadOnlyList<AdvertisementDto>>(ok.Value);

        var returned = Assert.Single(result);

        Assert.Equal(home.Id, returned.Id);
    }

    [Fact]
    public async Task InvalidPlacement_Returns400()
    {
        var controller = CreateController();

        var response = await controller.GetActive(
            "InvalidPlacement",
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            response.Result);
    }

    [Fact]
    public async Task ClientSuppliedTime_Returns400()
    {
        var controller = CreateController();

        controller.Request.QueryString =
            new QueryString(
                "?atUtc=2026-11-18T10:00:00Z");

        var response = await controller.GetActive(
            null,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            response.Result);
    }

    [Fact]
    public async Task DistrictQuery_Returns400()
    {
        var controller = CreateController();

        controller.Request.QueryString =
            new QueryString("?district=Jaffna");

        var response = await controller.GetActive(
            null,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            response.Result);
    }

    [Fact]
    public async Task ExpiredAdvertisement_IsExcluded()
    {
        var expired = CreateActiveAd() with
        {
            StartAtUtc = DateTimeOffset.UtcNow.AddHours(-3),
            EndAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var controller = CreateController(expired);

        var response = await controller.GetActive(
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var result = Assert.IsAssignableFrom<
            IReadOnlyList<AdvertisementDto>>(ok.Value);

        Assert.Empty(result);
    }
}
