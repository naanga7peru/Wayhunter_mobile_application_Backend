
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.TravelPlanning;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class RoutePreviewControllerTests
{
    private sealed class FakeRoutesProvider : IRoutesProvider
    {
        public int Calls { get; private set; }

        public RouteProviderResult Response { get; set; } =
            new(
                true,
                new[]
                {
                    new RouteWaypoint(9.66, 80.02),
                    new RouteWaypoint(9.67, 80.03)
                },
                2.5,
                5,
                "TestRoutes",
                null);

        public Task<RouteProviderResult> CalculateRouteAsync(
            IReadOnlyList<RouteWaypoint> orderedStops,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Response);
        }
    }

    private static RoutePreviewRequest ValidRequest() =>
        new(
            new[]
            {
                new RouteWaypoint(9.66, 80.02),
                new RouteWaypoint(9.67, 80.03)
            });

    private static RoutePreviewController CreateController(
        FakeRoutesProvider fake,
        bool authenticated = true,
        bool enabled = true)
    {
        var service = new PreviewTripRouteService(fake);

        var quota = new InMemoryRoutePreviewDailyQuota(
            TimeProvider.System);

        var options = Options.Create(new MapsProviderOptions
        {
            EnableRoutes = enabled,
            RoutesServerApiKey = "dummy-test-key"
        });

        var controller = new RoutePreviewController(
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

    private static Task<IActionResult> Preview(
        RoutePreviewController controller,
        RoutePreviewRequest? request = null)
    {
        return controller.Preview(
            request ?? ValidRequest(),
            CancellationToken.None);
    }

    [Fact]
    public async Task ValidRequest_Returns200()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(fake);

        var result = await Preview(controller);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task AnonymousUser_Returns401()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(
            fake,
            authenticated: false);

        var result = await Preview(controller);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task InvalidIdentity_Returns401()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(fake);

        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            "invalid-id")
                    },
                    "TestAuthentication"));

        var result = await Preview(controller);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task MissingStops_Returns400()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(fake);

        var request = new RoutePreviewRequest(
            Array.Empty<RouteWaypoint>());

        var result = await Preview(controller, request);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task InvalidGps_Returns400()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(fake);

        var request = new RoutePreviewRequest(
            new[]
            {
                new RouteWaypoint(110, 80),
                new RouteWaypoint(9.67, 80.03)
            });

        var result = await Preview(controller, request);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task DisabledRoutes_Returns503()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(
            fake,
            enabled: false);

        var result = await Preview(controller);

        var error = Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            error.StatusCode);

        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task ProviderFailure_Returns503()
    {
        var fake = new FakeRoutesProvider
        {
            Response = new RouteProviderResult(
                false,
                Array.Empty<RouteWaypoint>(),
                null,
                null,
                null,
                "Provider unavailable")
        };

        var controller = CreateController(fake);

        var result = await Preview(controller);

        var error = Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            error.StatusCode);
    }

    [Fact]
    public async Task InvalidProviderResponse_Returns502()
    {
        var fake = new FakeRoutesProvider
        {
            Response = new RouteProviderResult(
                true,
                Array.Empty<RouteWaypoint>(),
                2.5,
                5,
                "TestRoutes",
                null)
        };

        var controller = CreateController(fake);

        var result = await Preview(controller);

        var error = Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status502BadGateway,
            error.StatusCode);
    }

    [Fact]
    public async Task DailyLimit_Returns429On21stRequest()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(fake);

        for (var i = 0; i < 20; i++)
        {
            var result = await Preview(controller);

            Assert.IsType<OkObjectResult>(result);
        }

        var limited = await Preview(controller);

        var error = Assert.IsType<ObjectResult>(limited);

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            error.StatusCode);

        Assert.Equal(20, fake.Calls);
    }

    [Fact]
    public async Task ConflictingIdentityClaims_Returns401()
    {
        var fake = new FakeRoutesProvider();
        var controller = CreateController(fake);

        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            Guid.NewGuid().ToString("D")),
                        new Claim(
                            "sub",
                            Guid.NewGuid().ToString("D"))
                    },
                    "TestAuthentication"));

        var result = await Preview(controller);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, fake.Calls);
    }
}
