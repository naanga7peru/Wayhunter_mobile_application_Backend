
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class ReorderTripStopsHttpTests
{
    private const string TestScheme = "M2ReorderTest";

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(
                    (_, configuration) =>
                    {
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["Identity:EnableSharedAuthentication"]
                                    = "false",

                                ["TravelPlanning:EnableSqlPersistence"]
                                    = "false"
                            });
                    });

                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme =
                            TestScheme;

                        options.DefaultChallengeScheme =
                            TestScheme;
                    })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthHandler>(
                        TestScheme,
                        _ => { });
                });
            });
    }

    private static TripPlan CreateTrip()
    {
        var trip = new TripPlan(
            "user-001",
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3);

        trip.AddStop("Jaffna Fort");
        trip.AddStop("Nallur Temple");
        trip.AddStop("Point Pedro");

        return trip;
    }

    private static string Route(Guid tripId)
    {
        return $"/api/mobile/trip-plans/{tripId}/stops/order";
    }

    private static void Authenticate(
        HttpClient client,
        string userId)
    {
        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            userId);
    }

    private static async Task<TripPlan> SeedAsync(
        WebApplicationFactory<Program> factory)
    {
        var repo = factory.Services
            .GetRequiredService<ITripPlanRepository>();

        var trip = CreateTrip();

        await repo.AddAsync(trip);

        return trip;
    }

    // ==========================================
    // TEST 01 - ANONYMOUS ACCESS
    // ==========================================
    [Fact]
    public async Task AnonymousRequest_Returns401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var tripId = Guid.NewGuid();

        using var response = await client.PutAsJsonAsync(
            Route(tripId),
            new
            {
                stopIds = Array.Empty<Guid>()
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ==========================================
    // TEST 02 - VALID REORDER REQUEST
    // ==========================================
    [Fact]
    public async Task ValidOwner_ReordersStops_Returns200()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var trip = await SeedAsync(factory);

        Authenticate(client, "user-001");

        var stopIds = new[]
        {
            trip.Stops[2].Id,
            trip.Stops[0].Id,
            trip.Stops[1].Id
        };

        using var response = await client.PutAsJsonAsync(
            Route(trip.Id),
            new ReorderTripStopsDto(stopIds));

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<TripPlanResponseDto>();

        Assert.NotNull(result);

        Assert.Equal(
            new[]
            {
                "Point Pedro",
                "Jaffna Fort",
                "Nallur Temple"
            },
            result.Stops.Select(stop => stop.Name));

        Assert.Equal(
            new[] { 0, 1, 2 },
            result.Stops.Select(stop => stop.OrderIndex));
    }

    // ==========================================
    // TEST 03 - WRONG OWNER
    // ==========================================
    [Fact]
    public async Task DifferentOwner_Returns404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var trip = await SeedAsync(factory);

        Authenticate(client, "user-002");

        var originalOrder = trip.Stops
            .Select(stop => stop.Id)
            .ToArray();

        using var response = await client.PutAsJsonAsync(
            Route(trip.Id),
            new ReorderTripStopsDto(
                originalOrder.Reverse().ToArray()));

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            originalOrder,
            trip.Stops.Select(stop => stop.Id));
    }

    // ==========================================
    // TEST 04 - DUPLICATE STOP IDS
    // ==========================================
    [Fact]
    public async Task DuplicateStops_Returns400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var trip = await SeedAsync(factory);

        Authenticate(client, "user-001");

        var stopIds = new[]
        {
            trip.Stops[0].Id,
            trip.Stops[0].Id,
            trip.Stops[2].Id
        };

        using var response = await client.PutAsJsonAsync(
            Route(trip.Id),
            new ReorderTripStopsDto(stopIds));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "Nallur Temple",
            trip.Stops[1].Name);
    }

    // ==========================================
    // TEST 05 - CANCELLED TRIP
    // ==========================================
    [Fact]
    public async Task CancelledTrip_Returns409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var trip = await SeedAsync(factory);

        trip.Cancel();

        Authenticate(client, "user-001");

        using var response = await client.PutAsJsonAsync(
            Route(trip.Id),
            new ReorderTripStopsDto(
                trip.Stops
                    .Select(stop => stop.Id)
                    .Reverse()
                    .ToArray()));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    // ==========================================
    // TEST 06 - UNKNOWN TRIP
    // ==========================================
    [Fact]
    public async Task UnknownTrip_Returns404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        Authenticate(client, "user-001");

        using var response = await client.PutAsJsonAsync(
            Route(Guid.NewGuid()),
            new ReorderTripStopsDto(
                Array.Empty<Guid>()));

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // ==========================================
    // TEST-ONLY AUTHENTICATION HANDLER
    // ==========================================
    private sealed class TestAuthHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult>
            HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(
                    "X-Test-User",
                    out var values))
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var userId = values.ToString();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Task.FromResult(
                    AuthenticateResult.Fail(
                        "Missing test user ID."));
            }

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId)
            };

            var identity = new ClaimsIdentity(
                claims,
                TestScheme);

            var principal = new ClaimsPrincipal(identity);

            var ticket = new AuthenticationTicket(
                principal,
                TestScheme);

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
    }
}
