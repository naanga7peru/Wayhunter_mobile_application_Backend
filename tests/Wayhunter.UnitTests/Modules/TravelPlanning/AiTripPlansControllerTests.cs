
using System;
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
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class AiTripPlansControllerTests
{
    private static readonly string UserId =
        Guid.NewGuid().ToString("D");

    private const string ValidJson = """
    {
      "days": [
        {
          "date": "2026-11-20",
          "stops": [
            {
              "placeName": "Nallur",
              "estimatedCostLkr": 500
            }
          ]
        }
      ],
      "estimatedTotalCostLkr": 500,
      "requiresUserReview": true
    }
    """;

    private sealed class FakeProvider : IAiTripGenerationProvider
    {
        public int Calls { get; private set; }
        public string Json { get; set; } = ValidJson;

        public Task<string> GenerateItineraryJsonAsync(
            AiTripItineraryRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Json);
        }
    }

    private static AiTripItineraryRequest Request() => new(
        "Jaffna",
        "Nallur",
        new DateOnly(2026, 11, 20),
        new DateOnly(2026, 11, 21),
        10000m,
        "Car",
        2,
        null);

    private static AiTripPlansController Create(
        FakeProvider provider,
        bool authenticated = true,
        bool enabled = true,
        int maxRequests = 1)
    {
        var options = Options.Create(
            new AiTripProviderOptions
            {
                Enabled = enabled,
                ApiKey = "fake-test-key",
                Model = "test-model",
                TimeoutSeconds = 10,
                MaxRequestsPerUserPerDay = maxRequests
            });

        var controller = new AiTripPlansController(
            new GenerateAiTripPlanService(provider),
            new InMemoryAiTripDailyQuota(TimeProvider.System),
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
                            UserId)
                    },
                    "test"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        return controller;
    }

    [Fact]
    public async Task ValidRequest_ReturnsReviewableProposal()
    {
        var provider = new FakeProvider();
        var controller = Create(provider);

        var response = await controller.Generate(
            Request(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var proposal =
            Assert.IsType<AiTripItineraryResponse>(ok.Value);

        Assert.True(proposal.RequiresUserReview);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task AnonymousUser_IsRejected()
    {
        var provider = new FakeProvider();
        var controller = Create(
            provider, authenticated: false);

        var response = await controller.Generate(
            Request(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ConflictingIdentityClaims_AreRejected()
    {
        var provider = new FakeProvider();
        var controller = Create(provider);

        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier, UserId),
                    new Claim(
                        "sub", Guid.NewGuid().ToString("D"))
                },
                "test"));

        var response = await controller.Generate(
            Request(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task DailyLimit_RejectsSecondRequest()
    {
        var provider = new FakeProvider();
        var controller = Create(
            provider, maxRequests: 1);

        var first = await controller.Generate(
            Request(), CancellationToken.None);

        var second = await controller.Generate(
            Request(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);

        var limited = Assert.IsType<ObjectResult>(second);

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            limited.StatusCode);

        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task InvalidInput_DoesNotConsumeQuota()
    {
        var provider = new FakeProvider();
        var controller = Create(provider);

        var invalid = Request() with
        {
            StartLocation = ""
        };

        var first = await controller.Generate(
            invalid, CancellationToken.None);

        var second = await controller.Generate(
            Request(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(first);
        Assert.IsType<OkObjectResult>(second);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task DisabledAi_Returns503()
    {
        var provider = new FakeProvider();
        var controller = Create(
            provider, enabled: false);

        var response = await controller.Generate(
            Request(), CancellationToken.None);

        var unavailable =
            Assert.IsType<ObjectResult>(response);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            unavailable.StatusCode);

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task MalformedAiOutput_Returns502()
    {
        var provider = new FakeProvider
        {
            Json = "invalid-json"
        };

        var controller = Create(provider);

        var response = await controller.Generate(
            Request(), CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(response);

        Assert.Equal(
            StatusCodes.Status502BadGateway,
            error.StatusCode);
    }
}
