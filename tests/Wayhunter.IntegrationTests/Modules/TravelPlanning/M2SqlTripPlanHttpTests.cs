using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class M2SqlTripPlanHttpFactAttribute : FactAttribute
{
    public M2SqlTripPlanHttpFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M2_TEST_SQL_CONNECTION")))
            Skip = "Set the isolated M2 SQL test connection to run HTTP-to-SQL acceptance.";
    }
}

public sealed class M2SqlTripPlanHttpTests
{
    private const string Scheme = "M2SqlTest";
    private const string ConnectionEnvironmentVariable = "WAYHUNTER_M2_TEST_SQL_CONNECTION";

    [Fact]
    public async Task SwaggerListsTripPlanningAndSafetyOperations()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/mobile/trip-plans", out var plans));
        Assert.True(plans.TryGetProperty("get", out _));
        Assert.True(plans.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/mobile/trip-plans/{tripPlanId}/stops", out var stops));
        Assert.True(stops.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/mobile/trips/{tripPlanId}/sessions", out var sessions));
        Assert.True(sessions.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/mobile/trips/{tripPlanId}/memories", out var memories));
        Assert.True(memories.TryGetProperty("get", out _));

        var operationCount = paths.EnumerateObject().Sum(path => path.Value.EnumerateObject().Count());
        Assert.True(operationCount >= 20, $"Expected the M2/M3 API operations in Swagger, saw {operationCount}.");
    }

    [M2SqlTripPlanHttpFact]
    public async Task AuthenticatedOwner_CreatesReadsAndAddsSqlBackedStops()
    {
        var connectionString = GetConnectionString();
        var owner = UserAccount.RegisterPending("M2 SQL Owner", $"m2-sql-{Guid.NewGuid():N}@example.invalid", "test-password-hash", DateTimeOffset.UtcNow);
        owner.ActivateAfterVerifiedOtp(DateTimeOffset.UtcNow);

        var dbOptions = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(connectionString).Options;
        await using (var seed = new WayhunterSharedDbContext(dbOptions))
        {
            seed.Users.Add(owner);
            await seed.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:WayhunterSharedDb", connectionString);
            builder.UseSetting("TravelPlanning:EnableSqlPersistence", "true");
            builder.UseSetting("Identity:EnableSharedAuthentication", "false");
            builder.UseSetting("TripSafety:EnableSqlPersistence", "false");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WayhunterSharedDb"] = connectionString,
                ["TravelPlanning:EnableSqlPersistence"] = "true",
                ["Identity:EnableSharedAuthentication"] = "false",
                ["TripSafety:EnableSqlPersistence"] = "false"
            }));
            builder.ConfigureTestServices(services => services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(Scheme, _ => { }));
        });

        using (var scope = factory.Services.CreateScope())
            Assert.IsType<EfTripPlanRepository>(scope.ServiceProvider.GetRequiredService<Wayhunter.Application.Modules.TravelPlanning.Interfaces.ITripPlanRepository>());

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", owner.UserId);

        using var create = await client.PostAsJsonAsync("/api/mobile/trip-plans", new CreateTripPlanDto(
            "M2 SQL acceptance", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2), 25000m, "Car", 2)
        {
            InitialStops = [new InitialTripStopDto("Colombo Fort", "place-colombo-fort", 6.9344, 79.8428)]
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(created);
        var firstStop = Assert.Single(created.Stops);
        Assert.Equal("place-colombo-fort", firstStop.PlaceId);
        Assert.Equal(6.9344, firstStop.Latitude);
        Assert.Equal(79.8428, firstStop.Longitude);

        using var addStop = await client.PostAsJsonAsync($"/api/mobile/trip-plans/{created.TripPlanId}/stops", new AddTripStopDto(
            "Galle Face", "place-galle-face", 6.9271, 79.8447, created.RowVersion));
        Assert.Equal(HttpStatusCode.OK, addStop.StatusCode);

        using var read = await client.GetAsync($"/api/mobile/trip-plans/{created.TripPlanId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var persisted = await read.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Stops.Count);
        Assert.Equal(8, persisted.RowVersion.Length);
        Assert.Contains(persisted.Stops, stop => stop.PlaceId == "place-galle-face" && stop.Latitude == 6.9271 && stop.Longitude == 79.8447);

        using var update = await client.PutAsJsonAsync($"/api/mobile/trip-plans/{created.TripPlanId}", new UpdateTripPlanDto(
            "M2 SQL acceptance updated", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 3), 30000m, "Car", 3)
        {
            ExpectedRowVersion = persisted.RowVersion
        });
        Assert.True(update.StatusCode == HttpStatusCode.OK,
            $"Trip update failed: {(int)update.StatusCode} {await update.Content.ReadAsStringAsync()}");
        var updated = await update.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(updated);
        Assert.Equal("M2 SQL acceptance updated", updated.TripName);

        using var removeStop = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/mobile/trip-plans/{created.TripPlanId}/stops/{firstStop.StopId}");
        removeStop.Headers.TryAddWithoutValidation("If-Match", $"\"{Convert.ToBase64String(updated.RowVersion)}\"");
        using var removeStopResponse = await client.SendAsync(removeStop);
        Assert.Equal(HttpStatusCode.OK, removeStopResponse.StatusCode);
        var afterRemove = await removeStopResponse.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(afterRemove);
        Assert.Single(afterRemove.Stops);

        using var cancel = await client.DeleteAsync($"/api/mobile/trip-plans/{created.TripPlanId}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        using var outsideOwner = factory.CreateClient();
        outsideOwner.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString("D"));
        using var forbiddenRead = await outsideOwner.GetAsync($"/api/mobile/trip-plans/{created.TripPlanId}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenRead.StatusCode);
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("The M2 SQL connection is missing.");
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
            !builder.InitialCatalog.StartsWith("Wayhunter_Test_", StringComparison.OrdinalIgnoreCase) ||
            !builder.IntegratedSecurity)
            throw new InvalidOperationException("M2 SQL HTTP tests require an isolated Wayhunter_Test_ LocalDB catalog.");
        return builder.ConnectionString;
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var value) || string.IsNullOrWhiteSpace(value))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, value.ToString())], M2SqlTripPlanHttpTests.Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), M2SqlTripPlanHttpTests.Scheme)));
        }
    }
}
