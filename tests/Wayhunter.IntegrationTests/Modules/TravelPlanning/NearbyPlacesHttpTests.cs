
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class NearbyPlacesHttpTests
{
    private const string Scheme = "M2NearbyTest";

    private const string UserId =
        "2e43784a-d6b5-4f88-a723-1bcf30e83291";

    private const string ValidUrl =
        "/api/mobile/trip-plans/nearby" +
        "?latitude=9.66&longitude=80.02&category=food";

    private sealed class FakeProvider : INearbyPlacesProvider
    {
        public int Calls;

        public int LastRadius;

        public PlacesProviderResult Result { get; set; } =
            new(
                true,
                new[]
                {
                    new ExternalPlaceSuggestion(
                        "google-place-1",
                        "Test Restaurant",
                        9.661,
                        80.021,
                        "GooglePlaces")
                },
                null);

        public Task<PlacesProviderResult> SearchNearbyAsync(
            double latitude,
            double longitude,
            int radiusMeters,
            string searchTerm,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            LastRadius = radiusMeters;

            return Task.FromResult(Result);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        FakeProvider provider)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(
                    (_, config) =>
                    {
                        config.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["Identity:EnableSharedAuthentication"]
                                    = "false",

                                ["TravelPlanning:EnableSqlPersistence"]
                                    = "false",

                                ["TravelPlanning:Maps:EnablePlaces"]
                                    = "true",

                                ["TravelPlanning:Maps:PlacesServerApiKey"]
                                    = "dummy-test-key",

                                ["TravelPlanning:Maps:EnableRoutes"]
                                    = "false",

                                ["TravelPlanning:Maps:EnableGeocoding"]
                                    = "false"
                            });
                    });

                builder.ConfigureTestServices(services =>
                {
                    // Do not call Google's real HTTP API.
                    services.RemoveAll<INearbyPlacesProvider>();

                    services.AddSingleton<INearbyPlacesProvider>(
                        provider);

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = Scheme;
                        options.DefaultChallengeScheme = Scheme;
                    })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthHandler>(
                        Scheme,
                        _ => { });
                });
            });
    }

    private static void Authenticate(
        HttpClient client,
        string userId = UserId)
    {
        client.DefaultRequestHeaders.Add(
            "X-Test-User", userId);
    }

    [Fact]
    public async Task AnonymousRequest_Returns401()
    {
        var provider = new FakeProvider();

        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ValidUrl);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task InvalidIdentity_Returns401()
    {
        var provider = new FakeProvider();

        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        Authenticate(client, "invalid-user");

        using var response = await client.GetAsync(ValidUrl);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ValidRequest_Returns200WithPlaces()
    {
        var provider = new FakeProvider();

        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        Authenticate(client);

        using var response = await client.GetAsync(ValidUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(
            1000,
            root.GetProperty("radiusMeters").GetInt32());

        Assert.False(
            root.GetProperty("hunterVerified").GetBoolean());

        Assert.Single(
            root.GetProperty("places").EnumerateArray());

        Assert.Equal(1, provider.Calls);
        Assert.Equal(1000, provider.LastRadius);
    }

    [Fact]
    public async Task InvalidGps_Returns400()
    {
        var provider = new FakeProvider();

        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        Authenticate(client);

        var url =
            "/api/mobile/trip-plans/nearby" +
            "?latitude=120&longitude=80.02&category=food";

        using var response = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task InvalidCategory_Returns400()
    {
        var provider = new FakeProvider();

        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        Authenticate(client);

        var url =
            "/api/mobile/trip-plans/nearby" +
            "?latitude=9.66&longitude=80.02" +
            "&category=unknown";

        using var response = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task DailyLimit_Returns429On21stRequest()
    {
        var provider = new FakeProvider();

        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        Authenticate(client);

        for (var i = 0; i < 20; i++)
        {
            using var response = await client.GetAsync(ValidUrl);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }

        using var limited = await client.GetAsync(ValidUrl);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            limited.StatusCode);

        Assert.Equal(20, provider.Calls);
    }

    [Fact]
    public async Task ProviderFailure_Returns503()
    {
        var provider = new FakeProvider
        {
            Result = new PlacesProviderResult(
                false,
                Array.Empty<ExternalPlaceSuggestion>(),
                "Provider unavailable")
        };

        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        Authenticate(client);

        using var response = await client.GetAsync(ValidUrl);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);
    }

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

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId)
            };

            var identity = new ClaimsIdentity(
                claims,
                NearbyPlacesHttpTests.Scheme);

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                NearbyPlacesHttpTests.Scheme);

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
    }
}
