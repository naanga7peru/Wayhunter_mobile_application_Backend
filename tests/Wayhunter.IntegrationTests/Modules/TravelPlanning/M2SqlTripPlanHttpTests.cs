using System.Data.SqlTypes;
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
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
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

    [M2SqlTripPlanHttpFact]
    public async Task SuggestedTrips_SqlReaderHandlesEmptyListAndOrdersLimitsAndFiltersEligibleTrips()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(GetConnectionString()).Options;
        await using var db = new WayhunterSharedDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var reader = new SqlSuggestedTripPlansReader(db);
        var now = DateTimeOffset.UtcNow;
        var viewer = NewSuggestedTripUser(now);
        var owner = NewSuggestedTripUser(now);
        db.Users.AddRange(viewer, owner);
        await db.SaveChangesAsync();

        Assert.Empty(await reader.GetEligibleAsync(viewer.UserId, 20));

        var older = AddTrip("Older", owner.UserId);
        var firstTie = AddTrip("Newest A", owner.UserId);
        var secondTie = AddTrip("Newest B", owner.UserId);
        var ownTrip = AddTrip("Owned by viewer", viewer.UserId);
        var cancelled = AddTrip("Cancelled", owner.UserId);
        var removed = AddTrip("Removed membership", owner.UserId);
        var invitationOnly = AddTrip("Pending invitation", owner.UserId);
        var unshared = AddTrip("Unshared", owner.UserId);
        var unfinished = AddTrip("Unfinished", owner.UserId);
        var restarted = AddTrip("Restarted", owner.UserId);
        firstTie.AddStop("First stop");
        firstTie.AddStop("Second stop");

        foreach (var trip in new[] { older, firstTie, secondTie, cancelled, unfinished, restarted })
            AddInvitation(trip, accept: true);
        var removedMember = AddInvitation(removed, accept: true);
        removedMember!.Remove(now);
        AddInvitation(invitationOnly, accept: false);
        // Give the viewer membership even on their own trip to exercise the owner exclusion.
        AddInvitation(ownTrip, accept: true);
        cancelled.Cancel();

        var newest = now.AddMinutes(-10);
        var olderCompletion = now.AddMinutes(-20);
        AddFinishedSession(older, olderCompletion);
        AddFinishedSession(firstTie, now.AddMinutes(-30));
        AddFinishedSession(firstTie, newest);
        AddFinishedSession(secondTie, newest);
        foreach (var trip in new[] { ownTrip, cancelled, removed, invitationOnly, unshared, restarted })
            AddFinishedSession(trip, now.AddMinutes(-5));
        db.LiveTripSessions.Add(LiveTripSession.Start(Guid.NewGuid(), unfinished.Id, now));
        db.LiveTripSessions.Add(LiveTripSession.Start(Guid.NewGuid(), restarted.Id, now));
        await db.SaveChangesAsync();

        // SQL Server orders uniqueidentifier values differently from Guid.CompareTo.
        var orderedTies = new[] { firstTie.Id, secondTie.Id }.OrderBy(id => new SqlGuid(id)).ToArray();
        var results = await reader.GetEligibleAsync(viewer.UserId, 20);
        Assert.Equal(orderedTies.Append(older.Id).ToArray(), results.Select(plan => plan.TripPlanId).ToArray());
        Assert.Equal(new[] { newest, newest, olderCompletion }, results.Select(plan => plan.CompletedAtUtc).ToArray());
        Assert.Equal(orderedTies[0], Assert.Single(await reader.GetEligibleAsync(viewer.UserId, 1)).TripPlanId);

        var detail = await reader.GetEligibleByIdAsync(viewer.UserId, firstTie.Id);
        Assert.NotNull(detail);
        Assert.Equal(2, detail.StopCount);
        Assert.Equal(results.Single(plan => plan.TripPlanId == firstTie.Id), detail);
        Assert.Null(await reader.GetEligibleByIdAsync(viewer.UserId, removed.Id));
        await transaction.RollbackAsync();

        TripPlan AddTrip(string name, string ownerId)
        {
            var date = DateOnly.FromDateTime(now.UtcDateTime);
            var trip = new TripPlan(ownerId, name, date, date, 1000m, "Car", 2);
            db.Trips.Add(trip);
            return trip;
        }

        TripMember? AddInvitation(TripPlan trip, bool accept)
        {
            var invitation = TripInvitation.Create(Guid.NewGuid(), trip.Id, owner.UserId, viewer.UserId, now.AddHours(-2));
            db.TripInvitations.Add(invitation);
            if (!accept) return null;
            invitation.Accept(now.AddHours(-1));
            var member = TripMember.FromAcceptedInvitation(Guid.NewGuid(), invitation);
            db.TripMembers.Add(member);
            return member;
        }

        void AddFinishedSession(TripPlan trip, DateTimeOffset finishedAt)
        {
            var session = LiveTripSession.Start(Guid.NewGuid(), trip.Id, finishedAt.AddMinutes(-1));
            session.Finish(finishedAt);
            db.LiveTripSessions.Add(session);
        }
    }

    private static UserAccount NewSuggestedTripUser(DateTimeOffset now)
    {
        var user = UserAccount.RegisterPending("Suggested trip test", $"m2-{Guid.NewGuid():N}@example.invalid", "test-password-hash", now);
        user.ActivateAfterVerifiedOtp(now);
        return user;
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
