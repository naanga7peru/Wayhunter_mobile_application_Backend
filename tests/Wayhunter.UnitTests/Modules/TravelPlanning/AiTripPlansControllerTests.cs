
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.TravelPlanning;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Ai;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
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
        public Exception? Failure { get; init; }

        public Task<string> GenerateItineraryJsonAsync(
            AiTripItineraryRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Failure is { } failure
                ? Task.FromException<string>(failure)
                : Task.FromResult(Json);
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

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("""{"days":[],"requiresUserReview":true}""")]
    public async Task MalformedAiOutput_Returns502(string json)
    {
        var provider = new FakeProvider
        {
            Json = json
        };

        var controller = Create(provider);

        var response = await controller.Generate(
            Request(), CancellationToken.None);

        AssertCodeOnly(response, StatusCodes.Status502BadGateway, "INVALID_AI_RESPONSE");
    }

    [Theory]
    [InlineData(true, StatusCodes.Status504GatewayTimeout, "AI_PROVIDER_TIMEOUT")]
    [InlineData(false, StatusCodes.Status502BadGateway, "AI_PROVIDER_FAILED")]
    public async Task ProviderFailure_ReturnsSanitizedError(bool timeout, int status, string code)
    {
        var provider = new FakeProvider
        {
            Failure = timeout
                ? new TimeoutException("Private provider detail")
                : new HttpRequestException("Private provider detail")
        };

        var response = await Create(provider).Generate(Request(), CancellationToken.None);

        AssertCodeOnly(response, status, code);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ReviewedProposal_UsesNormalDraftCreationWithEditedBudget()
    {
        var repository = new InMemoryTripPlanRepository();
        var draftService = new CreateTripPlanService(repository);
        var request = Request();
        var response = await Create(new FakeProvider()).Generate(request, CancellationToken.None);
        var proposal = Assert.IsType<AiTripItineraryResponse>(Assert.IsType<OkObjectResult>(response).Value);
        var proposalBefore = JsonSerializer.Serialize(proposal);
        Assert.True(proposal.RequiresUserReview);
        Assert.Empty(await repository.GetByOwnerAsync(UserId));

        // Model the reviewed client input via the normal create contract.
        var reviewed = new CreateTripPlanDto("Reviewed trip", request.StartDate,
            request.EndDate.AddDays(1), 750m, request.VehicleType, request.Headcount)
        {
            InitialStops = proposal.Days.SelectMany(day => day.Stops)
                .Select(stop => new InitialTripStopDto(stop.PlaceName, stop.ProviderPlaceId))
                .ToArray()
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            draftService.CreateAsync(reviewed with { BudgetLkr = -1m }, UserId));
        Assert.Empty(await repository.GetByOwnerAsync(UserId));

        var draft = await draftService.CreateAsync(reviewed, UserId);

        var saved = Assert.Single(await repository.GetByOwnerAsync(UserId));
        Assert.Equal(draft.TripPlanId, saved.Id);
        Assert.Equal(TripDraftStatus.Draft, saved.DraftStatus);
        Assert.Equal(750m, saved.BudgetLkr);
        Assert.Equal(reviewed.EndDate, saved.EndDate);
        Assert.Equal("Nallur", Assert.Single(saved.Stops).Name);
        Assert.Equal(proposalBefore, JsonSerializer.Serialize(proposal));
    }

    private static void AssertCodeOnly(IActionResult result, int status, string code)
    {
        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, error.StatusCode);
        var field = Assert.Single(JsonSerializer.SerializeToElement(error.Value).EnumerateObject());
        Assert.Equal("code", field.Name);
        Assert.Equal(code, field.Value.GetString());
    }
}
