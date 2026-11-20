
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.TravelPlanning;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class NearbyPlacesControllerTests
{
    private sealed class FakeNearbyProvider
        : INearbyPlacesProvider
    {
        public int Calls { get; private set; }
        public int LastRadius { get; private set; }
        public string? LastSearchTerm { get; private set; }

        public PlacesProviderResult Result { get; set; } =
            new(
                true,
                new[]
                {
                    new ExternalPlaceSuggestion(
                        "test-place-1",
                        "Jaffna Restaurant",
                        9.661,
                        80.021,
                        "TestProvider")
                },
                null);

        public Task<PlacesProviderResult> SearchNearbyAsync(
            double latitude,
            double longitude,
            int radiusMeters,
            string searchTerm,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRadius = radiusMeters;
            LastSearchTerm = searchTerm;

            return Task.FromResult(Result);
        }
    }

    private static NearbyPlacesController Create(
        FakeNearbyProvider provider,
        bool authenticated = true,
        bool enabled = true)
    {
        var service = new NearbyPlaceSearchService(provider);

        var quota = new InMemoryNearbyPlacesDailyQuota(
            TimeProvider.System);

        var options = Options.Create(new MapsProviderOptions
        {
            EnablePlaces = enabled,
            PlacesServerApiKey = "fake-test-key"
        });

        var controller = new NearbyPlacesController(
            service,
            quota,
            options);

        var context = new DefaultHttpContext();

        if (authenticated)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            Guid.NewGuid().ToString("D"))
                    },
                    "TestAuthentication"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        return controller;
    }

    private static Task<IActionResult> Search(
        NearbyPlacesController controller,
        double? lat = 9.66,
        double? lon = 80.02,
        string? category = "food")
    {
        return controller.FindNearby(
            lat,
            lon,
            category,
            CancellationToken.None);
    }

    [Fact]
    public async Task ValidSearch_Returns200()
    {
        var provider = new FakeNearbyProvider();
        var controller = Create(provider);

        var result = await Search(controller);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1000, provider.LastRadius);
        Assert.Equal("restaurant", provider.LastSearchTerm);
    }

    [Fact]
    public async Task AnonymousUser_Returns401()
    {
        var provider = new FakeNearbyProvider();

        var controller = Create(
            provider,
            authenticated: false);

        var result = await Search(controller);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ConflictingIdentityClaims_Return401()
    {
        var provider = new FakeNearbyProvider();
        var controller = Create(provider);

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                Guid.NewGuid().ToString("D")),

            new Claim(
                "sub",
                Guid.NewGuid().ToString("D"))
        };

        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(
                new ClaimsIdentity(claims, "TestAuthentication"));

        var result = await Search(controller);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task MissingGps_Returns400()
    {
        var provider = new FakeNearbyProvider();
        var controller = Create(provider);

        var result = await Search(
            controller,
            lat: null);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task InvalidGps_Returns400()
    {
        var provider = new FakeNearbyProvider();
        var controller = Create(provider);

        var result = await Search(
            controller,
            lat: 150);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task InvalidCategory_Returns400()
    {
        var provider = new FakeNearbyProvider();
        var controller = Create(provider);

        var result = await Search(
            controller,
            category: "unknown-category");

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task DisabledProvider_Returns503()
    {
        var provider = new FakeNearbyProvider();

        var controller = Create(
            provider,
            enabled: false);

        var result = await Search(controller);

        var error = Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            error.StatusCode);

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task DailyQuota_Rejects21stRequest()
    {
        var provider = new FakeNearbyProvider();
        var controller = Create(provider);

        for (var i = 0; i < 20; i++)
        {
            var result = await Search(controller);
            Assert.IsType<OkObjectResult>(result);
        }

        var limited = await Search(controller);

        var error = Assert.IsType<ObjectResult>(limited);

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            error.StatusCode);

        Assert.Equal(20, provider.Calls);
    }

    [Fact]
    public async Task ProviderFailure_Returns503()
    {
        var provider = new FakeNearbyProvider
        {
            Result = new PlacesProviderResult(
                false,
                Array.Empty<ExternalPlaceSuggestion>(),
                "Provider unavailable")
        };

        var controller = Create(provider);

        var result = await Search(controller);

        var error = Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            error.StatusCode);
    }

    [Fact]
    public async Task InvalidProviderResult_Returns502()
    {
        var provider = new FakeNearbyProvider
        {
            Result = new PlacesProviderResult(
                true,
                Array.Empty<ExternalPlaceSuggestion>(),
                "Unexpected failure")
        };

        var controller = Create(provider);

        var result = await Search(controller);

        var error = Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status502BadGateway,
            error.StatusCode);
    }
}
