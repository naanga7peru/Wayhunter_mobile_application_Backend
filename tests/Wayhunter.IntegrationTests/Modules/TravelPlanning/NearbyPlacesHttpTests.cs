
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

    [Theory]
    [InlineData("/api/mobile/trip-plans/weather?latitude=9.66&longitude=80.02")]
    [InlineData("/api/mobile/trip-plans/places/search?query=Jaffna")]
    [InlineData("/api/mobile/trip-plans/places/google-place-1")]
    public async Task PickerAndWeather_RequireAuthentication(string url)
    {
        using var factory = CreateFactory(new FakeProvider());
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/mobile/trip-plans/weather?longitude=80.02")]
    [InlineData("/api/mobile/trip-plans/weather?latitude=91&longitude=80.02")]
    [InlineData("/api/mobile/trip-plans/places/search?query=%20")]
    [InlineData("/api/mobile/trip-plans/places/invalid!")]
    public async Task PickerAndWeather_RejectInvalidInput(string url)
    {
        using var factory = CreateFactory(new FakeProvider());
        using var client = factory.CreateClient();
        Authenticate(client);
        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WeatherDisabled_ReturnsUnavailable()
    {
        using var baseFactory = CreateFactory(new FakeProvider());
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["TravelPlanning:Weather:Enabled"] = "false" })));
        using var client = factory.CreateClient();
        Authenticate(client);
        using var response = await client.GetAsync(
            "/api/mobile/trip-plans/weather?latitude=9.66&longitude=80.02");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("WEATHER_UNAVAILABLE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RoutePreviewDisabled_ReturnsUnavailable()
    {
        using var factory = CreateFactory(new FakeProvider());
        using var client = factory.CreateClient();
        Authenticate(client);
        using var content = new StringContent(
            """{"orderedStops":[{"latitude":9.66,"longitude":80.02},{"latitude":9.67,"longitude":80.03}]}""",
            System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/mobile/trip-plans/routes/preview", content);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
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

    [Fact]
    public async Task SuggestedDetail_UsesTrustedViewerAndCurrentEligibility()
    {
        var reader = new DetailReader();
        using var original = CreateFactory(new FakeProvider());
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISuggestedTripPlansReader>();
            services.AddSingleton<ISuggestedTripPlansReader>(reader);
        }));
        using var client = factory.CreateClient();
        Authenticate(client);
        var path = "/api/mobile/trip-plans/suggested/" + reader.Id;

        using var allowed = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.True(allowed.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
        Assert.Equal(reader.Id, document.RootElement.GetProperty("tripPlanId").GetGuid());
        Assert.False(document.RootElement.TryGetProperty("stops", out _));
        Assert.False(document.RootElement.TryGetProperty("ownerUserId", out _));

        reader.Eligible = false;
        using var revoked = await client.GetAsync(path);
        using var missing = await client.GetAsync("/api/mobile/trip-plans/suggested/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        Assert.Equal(missing.StatusCode, revoked.StatusCode);
        using var missingBody = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        using var revokedBody = JsonDocument.Parse(await revoked.Content.ReadAsStringAsync());
        Assert.Equal(
            missingBody.RootElement.EnumerateObject().Where(field => field.Name != "traceId")
                .Select(field => field.ToString()),
            revokedBody.RootElement.EnumerateObject().Where(field => field.Name != "traceId")
                .Select(field => field.ToString()));
    }

    [Theory]
    [InlineData("/api/mobile/trip-plans/suggested")]
    [InlineData("/api/mobile/trip-plans/suggested/ce8487c2-6a96-4045-aff2-b4e0797f36cf")]
    public async Task SuggestedReads_RejectMalformedTrustedIdentity(string path)
    {
        using var factory = CreateFactory(new FakeProvider());
        using var client = factory.CreateClient();
        Authenticate(client, "malformed-user");
        using var result = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed-user")]
    public async Task SuggestedCopy_RejectsMissingOrMalformedTrustedIdentity(string? userId)
    {
        using var factory = CreateFactory(new FakeProvider());
        using var client = factory.CreateClient();
        if (userId is not null)
            Authenticate(client, userId);

        using var result = await client.PostAsync(
            "/api/mobile/trip-plans/suggested/" + Guid.NewGuid() + "/copy", null);

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task SuggestedCopy_RepeatedRequestWhileDisabled_CreatesNoDraft()
    {
        using var factory = CreateFactory(new FakeProvider());
        using var client = factory.CreateClient();
        Authenticate(client);
        var url = "/api/mobile/trip-plans/suggested/" + Guid.NewGuid() + "/copy";

        for (var i = 0; i < 2; i++)
        {
            using var result = await client.PostAsync(url, null);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        var repository = factory.Services.GetRequiredService<ITripPlanRepository>();
        Assert.Empty(await repository.GetByOwnerAsync(UserId));
    }

    [Fact]
    public async Task Forecast_RequiresIdentityAndReturnsDatedFuturePoints()
    {
        using var original = CreateFactory(new FakeProvider());
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWeatherForecastProvider>();
            services.AddSingleton<IWeatherForecastProvider>(new ForecastProvider());
        }));
        using var client = factory.CreateClient();
        const string url = "/api/mobile/trip-plans/weather/forecast?latitude=9.66&longitude=80.02";

        using var anonymous = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Authenticate(client);
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = document.RootElement;
        Assert.Equal("fresh", result.GetProperty("status").GetString());
        Assert.Equal("OpenWeatherMap", result.GetProperty("providerName").GetString());
        var point = Assert.Single(result.GetProperty("forecasts").EnumerateArray());
        Assert.True(point.GetProperty("forecastAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task PlacePicker_EnforcesOneBudgetAcrossSearchAndDetails()
    {
        var provider = new PickerProvider();
        using var original = CreateFactory(new FakeProvider());
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPlacesProvider>();
            services.RemoveAll<IPlaceDetailsProvider>();
            services.AddSingleton<IPlacesProvider>(provider);
            services.AddSingleton<IPlaceDetailsProvider>(provider);
        }));
        using var client = factory.CreateClient();
        Authenticate(client);
        using var details = await client.GetAsync("/api/mobile/trip-plans/places/google-place-1");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        for (var i = 0; i < 59; i++)
        {
            using var result = await client.GetAsync("/api/mobile/trip-plans/places/search?query=Jaffna");
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }
        using var denied = await client.GetAsync("/api/mobile/trip-plans/places/google-place-1");
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.Equal(60, provider.Calls);
    }

    private sealed class PickerProvider : IPlacesProvider, IPlaceDetailsProvider
    {
        public int Calls { get; private set; }

        public Task<PlacesProviderResult> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new PlacesProviderResult(true,
                [new("google-place-1", "Jaffna", 9.66, 80.02, "GooglePlaces")], null));
        }

        public Task<PlacesProviderResult> GetDetailsAsync(
            string providerPlaceId, CancellationToken cancellationToken = default) =>
            SearchAsync(providerPlaceId, cancellationToken);
    }

    private sealed class ForecastProvider : IWeatherForecastProvider
    {
        public Task<IReadOnlyList<WeatherForecastPoint>?> GetForecastAsync(
            double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WeatherForecastPoint>?>([
                new(DateTimeOffset.UtcNow.AddHours(-3), 26, "Clouds"),
                new(DateTimeOffset.UtcNow.AddHours(3), 28, "Clear")
            ]);
    }

    private sealed class DetailReader : ISuggestedTripPlansReader
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Eligible { get; set; } = true;

        public Task<Wayhunter.Application.Modules.TravelPlanning.Dtos.SuggestedTripPlanDto?> GetEligibleByIdAsync(
            string viewer, Guid id, CancellationToken cancellationToken = default)
        {
            Assert.Equal(UserId, viewer);
            Wayhunter.Application.Modules.TravelPlanning.Dtos.SuggestedTripPlanDto? result =
                Eligible && id == Id
                    ? new(Id, "Public summary fields", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2),
                        100, "Car", 2, 3, DateTimeOffset.UtcNow)
                    : null;
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Wayhunter.Application.Modules.TravelPlanning.Dtos.SuggestedTripPlanDto>>
            GetEligibleAsync(string authenticatedUserId, int maxResults, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Detail must not enumerate the list.");
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
