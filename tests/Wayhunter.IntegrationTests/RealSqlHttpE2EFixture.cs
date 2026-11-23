using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Infrastructure.Integrations.Email;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.IntegrationTests;

/// <summary>
/// Opt-in authenticated SQL HTTP acceptance tests. Run only against an isolated
/// LocalDB catalog created and owned by this fixture; never uses appsettings DBs.
/// </summary>
public sealed class RealSqlHttpE2EFixture : IAsyncLifetime
{
    private readonly string _databaseName = "WayhunterE2E_" + Guid.NewGuid().ToString("N");
    private WebApplicationFactory<Program>? _factory;
    private string? _connectionString;
    private bool _created;

    public HttpClient Client { get; private set; } = null!;
    public string? OtpCode { get; private set; }
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        var master = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true
        }.ConnectionString;
        await using (var connection = new SqlConnection(master))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
            _created = true;
        }

        _connectionString = new SqlConnectionStringBuilder(master)
        {
            InitialCatalog = _databaseName
        }.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:WayhunterSharedDb", ConnectionString);
            builder.UseSetting("Identity:EnableSharedAuthentication", "true");
            builder.UseSetting("TravelPlanning:EnableSqlPersistence", "true");
            builder.UseSetting("TripSafety:EnableSqlPersistence", "true");
            builder.UseSetting("TripSafety:EnableSqlTripMemories", "true");
            builder.UseSetting("Games:EnableSqlPersistence", "true");
            builder.UseSetting("Games:EnableSqlAttemptWriter", "true");
            builder.UseSetting("Advertisements:EnableSqlPersistence", "true");
            builder.UseSetting("CoinsWallet:EnableSqlRewardWriter", "false");
            builder.UseSetting("CoinsWallet:EnableSqlRewardOutboxStore", "false");
            builder.UseSetting("CoinsWallet:EnableRewardOutboxWorker", "false");
            builder.UseSetting("CoinsWallet:EnableSqlVoucherRuntime", "false");
            builder.UseSetting("Authentication:Jwt:SigningSecret", "e2e-only-signing-secret-with-at-least-32-bytes");
            builder.UseSetting("Authentication:Jwt:Issuer", "wayhunter-sql-e2e");
            builder.UseSetting("Authentication:Jwt:Audience", "wayhunter-sql-e2e-mobile");
            builder.UseSetting("Identity:SessionLifetimeMinutes", "60");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WayhunterSharedDb"] = ConnectionString,
                    ["Identity:EnableSharedAuthentication"] = "true",
                    ["TravelPlanning:EnableSqlPersistence"] = "true",
                    ["TripSafety:EnableSqlPersistence"] = "true",
                    ["TripSafety:EnableSqlTripMemories"] = "true",
                    ["TripSafety:EnableSafeCheckIn"] = "true",
                    ["Games:EnableSqlPersistence"] = "true",
                    ["Games:EnableSqlAttemptWriter"] = "true",
                    ["Advertisements:EnableSqlPersistence"] = "true",
                    ["CoinsWallet:EnableSqlRewardWriter"] = "false",
                    ["CoinsWallet:EnableSqlRewardOutboxStore"] = "false",
                    ["CoinsWallet:EnableRewardOutboxWorker"] = "false",
                    ["CoinsWallet:EnableSqlVoucherRuntime"] = "false",
                    ["Authentication:Jwt:SigningSecret"] = "e2e-only-signing-secret-with-at-least-32-bytes",
                    ["Authentication:Jwt:Issuer"] = "wayhunter-sql-e2e",
                    ["Authentication:Jwt:Audience"] = "wayhunter-sql-e2e-mobile",
                    ["Identity:SessionLifetimeMinutes"] = "60"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentityOtpEmailSender>();
                services.AddSingleton<IIdentityOtpEmailSender>(new CapturingOtpSender(code => OtpCode = code));
            });
        });

        // Apply checked-in migrations only to this fixture-owned catalog.
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(ConnectionString).Options;
        await using (var db = new WayhunterSharedDbContext(options))
            await db.Database.MigrateAsync();
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_created)
        {
            var master = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
            _created = false;
        }
    }

    public async Task<string> RegisterAndLoginAsync(string email = "sql-e2e@wayhunter.invalid")
        => await RegisterAndLoginAsync(Client, email);

    public async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        const string password = "E2e-Password-2026!";
        using var register = await client.PostAsJsonAsync("/api/mobile/identity/register",
            new { displayName = "SQL E2E", email, password, confirmPassword = password });
        Assert.True(register.StatusCode == HttpStatusCode.Created,
            $"Registration returned {(int)register.StatusCode}: {await register.Content.ReadAsStringAsync()}");
        Assert.False(string.IsNullOrWhiteSpace(OtpCode));
        using var verify = await client.PostAsJsonAsync("/api/mobile/identity/verify-otp", new { email, code = OtpCode });
        Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
        Assert.True(login.StatusCode == HttpStatusCode.OK,
            $"Login returned {(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
        using var payload = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = payload.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token!;
    }

    [SqlE2EFact]
    public async Task M1BlocksAndM3InvitationsUseAuthenticatedSqlEndToEnd()
    {
        await RegisterAndLoginAsync();
        var recipientClient = _factory!.CreateClient();
        await RegisterAndLoginAsync(recipientClient, "sql-e2e-recipient@wayhunter.invalid");

        await using var db = CreateVerificationContext();
        var ownerId = await db.Users.Where(x => x.NormalizedEmail == "sql-e2e@wayhunter.invalid")
            .Select(x => x.UserId).SingleAsync();
        var recipientId = await db.Users.Where(x => x.NormalizedEmail == "sql-e2e-recipient@wayhunter.invalid")
            .Select(x => x.UserId).SingleAsync();

        using var block = await Client.PostAsync($"/api/mobile/identity/blocks/{recipientId}", null);
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);
        using var repeatBlock = await Client.PostAsync($"/api/mobile/identity/blocks/{recipientId}", null);
        Assert.Equal(HttpStatusCode.NoContent, repeatBlock.StatusCode);
        Assert.Equal(1, await db.BlockedUserRelationships.CountAsync());

        var blockedTripId = await CreateSqlTripAsync();
        using var blockedInvite = await Client.PostAsJsonAsync(
            $"/api/mobile/trips/{blockedTripId}/invitations", new { recipientUserId = recipientId });
        Assert.Equal(HttpStatusCode.Forbidden, blockedInvite.StatusCode);
        Assert.Empty(await db.TripInvitations.ToListAsync());

        // A block in the reverse direction is also authoritative.
        using var recipientBlocksOwner = await recipientClient.PostAsync($"/api/mobile/identity/blocks/{ownerId}", null);
        Assert.Equal(HttpStatusCode.NoContent, recipientBlocksOwner.StatusCode);
        using var blockedFromOtherDirection = await Client.PostAsJsonAsync(
            $"/api/mobile/trips/{blockedTripId}/invitations", new { recipientUserId = recipientId });
        Assert.Equal(HttpStatusCode.Forbidden, blockedFromOtherDirection.StatusCode);
        using var recipientUnblock = await recipientClient.DeleteAsync($"/api/mobile/identity/blocks/{ownerId}");
        Assert.Equal(HttpStatusCode.NoContent, recipientUnblock.StatusCode);
        using var ownerUnblock = await Client.DeleteAsync($"/api/mobile/identity/blocks/{recipientId}");
        Assert.Equal(HttpStatusCode.NoContent, ownerUnblock.StatusCode);
        using var repeatUnblock = await Client.DeleteAsync($"/api/mobile/identity/blocks/{recipientId}");
        Assert.Equal(HttpStatusCode.NoContent, repeatUnblock.StatusCode);
        Assert.Empty(await db.BlockedUserRelationships.ToListAsync());

        var acceptedTripId = await CreateSqlTripAsync();
        using var createAccepted = await Client.PostAsJsonAsync(
            $"/api/mobile/trips/{acceptedTripId}/invitations", new { recipientUserId = recipientId });
        Assert.Equal(HttpStatusCode.Created, createAccepted.StatusCode);
        using var acceptedPayload = JsonDocument.Parse(await createAccepted.Content.ReadAsStringAsync());
        var acceptedId = acceptedPayload.RootElement.GetProperty("invitationId").GetGuid();
        using var createRetry = await Client.PostAsJsonAsync(
            $"/api/mobile/trips/{acceptedTripId}/invitations", new { recipientUserId = recipientId });
        Assert.Equal(HttpStatusCode.Conflict, createRetry.StatusCode);

        using var ownerCannotAccept = await Client.PostAsync($"/api/mobile/trip-invitations/{acceptedId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, ownerCannotAccept.StatusCode);
        using var lateBlock = await Client.PostAsync($"/api/mobile/identity/blocks/{recipientId}", null);
        Assert.Equal(HttpStatusCode.NoContent, lateBlock.StatusCode);
        using var blockedAcceptance = await recipientClient.PostAsync($"/api/mobile/trip-invitations/{acceptedId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, blockedAcceptance.StatusCode);
        using var lateUnblock = await Client.DeleteAsync($"/api/mobile/identity/blocks/{recipientId}");
        Assert.Equal(HttpStatusCode.NoContent, lateUnblock.StatusCode);
        using var accepted = await recipientClient.PostAsync($"/api/mobile/trip-invitations/{acceptedId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptRetry = await recipientClient.PostAsync($"/api/mobile/trip-invitations/{acceptedId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptRetry.StatusCode);

        var rejectedTripId = await CreateSqlTripAsync();
        using var createRejected = await Client.PostAsJsonAsync(
            $"/api/mobile/trips/{rejectedTripId}/invitations", new { recipientUserId = recipientId });
        Assert.Equal(HttpStatusCode.Created, createRejected.StatusCode);
        using var rejectedPayload = JsonDocument.Parse(await createRejected.Content.ReadAsStringAsync());
        var rejectedId = rejectedPayload.RootElement.GetProperty("invitationId").GetGuid();
        using var rejected = await recipientClient.PostAsync($"/api/mobile/trip-invitations/{rejectedId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        using var rejectRetry = await recipientClient.PostAsync($"/api/mobile/trip-invitations/{rejectedId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejectRetry.StatusCode);

        var cancelledTripId = await CreateSqlTripAsync();
        using var createCancelled = await Client.PostAsJsonAsync(
            $"/api/mobile/trips/{cancelledTripId}/invitations", new { recipientUserId = recipientId });
        Assert.Equal(HttpStatusCode.Created, createCancelled.StatusCode);
        using var cancelledPayload = JsonDocument.Parse(await createCancelled.Content.ReadAsStringAsync());
        var cancelledId = cancelledPayload.RootElement.GetProperty("invitationId").GetGuid();
        using var cancelled = await Client.PostAsync($"/api/mobile/trip-invitations/{cancelledId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var cancelRetry = await Client.PostAsync($"/api/mobile/trip-invitations/{cancelledId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelRetry.StatusCode);

        Assert.Equal(3, await db.TripInvitations.CountAsync());
        Assert.Equal(1, await db.TripMembers.CountAsync(x => x.InvitationId == acceptedId));
        Assert.Equal(Wayhunter.Domain.Modules.TripSafety.Enums.TripInvitationStatus.Accepted,
            await db.TripInvitations.Where(x => x.Id == acceptedId).Select(x => x.Status).SingleAsync());
        Assert.Equal(Wayhunter.Domain.Modules.TripSafety.Enums.TripInvitationStatus.Rejected,
            await db.TripInvitations.Where(x => x.Id == rejectedId).Select(x => x.Status).SingleAsync());
        Assert.Equal(Wayhunter.Domain.Modules.TripSafety.Enums.TripInvitationStatus.Cancelled,
            await db.TripInvitations.Where(x => x.Id == cancelledId).Select(x => x.Status).SingleAsync());
        recipientClient.Dispose();
    }

    private async Task<Guid> CreateSqlTripAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var response = await Client.PostAsJsonAsync("/api/mobile/trip-plans",
            new CreateTripPlanDto("Invitation SQL trip", today.AddDays(1), today.AddDays(2), 10000m, "Car", 2));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var trip = await response.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(trip);
        return trip.TripPlanId;
    }

    [SqlE2EFact]
    public async Task M1_RegistrationOtpLoginAndProfileUseSqlAndRealBearer()
    {
        await RegisterAndLoginAsync();
        using var me = await Client.GetAsync("/api/mobile/identity/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var profileUpdate = await Client.PutAsJsonAsync(
            "/api/mobile/identity/me",
            new { displayName = "Updated SQL E2E", bio = "Persisted over authenticated HTTP" });
        Assert.Equal(HttpStatusCode.OK, profileUpdate.StatusCode);
        await using var db = CreateVerificationContext();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "sql-e2e@wayhunter.invalid"));
        Assert.Equal("Updated SQL E2E", await db.Users
            .Where(x => x.NormalizedEmail == "sql-e2e@wayhunter.invalid")
            .Select(x => x.DisplayName).SingleAsync());
        Assert.Equal("Persisted over authenticated HTTP", await db.UserProfiles
            .Where(x => db.Users.Any(u => u.UserId == x.UserId && u.NormalizedEmail == "sql-e2e@wayhunter.invalid"))
            .Select(x => x.Bio).SingleAsync());
        Assert.Equal(1, await db.IdentitySessions.CountAsync());
    }

    [SqlE2EFact]
    public async Task M2_AuthenticatedTripCrudPersistsInSql()
    {
        await RegisterAndLoginAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var createRequest = new CreateTripPlanDto(
            "HTTP SQL trip", today.AddDays(1), today.AddDays(3), 25000m, "Car", 2)
        {
            InitialStops =
            [
                new InitialTripStopDto("SQL origin", "place-origin", 9.661, 80.021),
                new InitialTripStopDto("SQL destination", "place-destination", 9.671, 80.031)
            ]
        };
        using var create = await Client.PostAsJsonAsync("/api/mobile/trip-plans", createRequest);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var trip = await create.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(trip);
        Assert.Equal(2, trip.Stops.Count);
        Assert.Equal(9.661, trip.Stops[0].Latitude);
        Assert.Equal(80.021, trip.Stops[0].Longitude);
        using var list = await Client.GetAsync("/api/mobile/trip-plans");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var detail = await Client.GetAsync($"/api/mobile/trip-plans/{trip.TripPlanId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var updateRequest = new UpdateTripPlanDto("Updated HTTP SQL trip", today.AddDays(1), today.AddDays(4), 30000m, "Car", 2)
        { ExpectedRowVersion = trip.RowVersion };
        var updated = await Client.PutAsJsonAsync($"/api/mobile/trip-plans/{trip.TripPlanId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await using var db = CreateVerificationContext();
        Assert.Equal("Updated HTTP SQL trip", await db.Trips.Where(x => x.Id == trip.TripPlanId).Select(x => x.TripName).SingleAsync());
        var persistedStops = await db.TripStops
            .Where(x => x.TripPlanId == trip.TripPlanId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync();
        Assert.Equal(2, persistedStops.Count);
        Assert.Equal(9.661, persistedStops[0].Latitude);
        Assert.Equal(80.021, persistedStops[0].Longitude);
        Assert.Equal("place-destination", persistedStops[1].PlaceId);
    }

    [SqlE2EFact]
    public async Task M3_AuthenticatedTripMemoriesPersistAndSoftDeleteInSql()
    {
        await RegisterAndLoginAsync("memory-sql-e2e@wayhunter.invalid");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var tripResponse = await Client.PostAsJsonAsync("/api/mobile/trip-plans",
            new CreateTripPlanDto("Memory HTTP SQL trip", today.AddDays(1), today.AddDays(3), 25000m, "Car", 2)
            {
                InitialStops = [new InitialTripStopDto("Memory stop", "memory-stop", 9.661, 80.021)]
            });
        Assert.Equal(HttpStatusCode.Created, tripResponse.StatusCode);
        var trip = await tripResponse.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(trip);
        var operationId = Guid.NewGuid();
        using var create = await Client.PostAsJsonAsync($"/api/mobile/trips/{trip.TripPlanId}/memories",
            new { operationId, tripStopId = trip.Stops[0].StopId, caption = "private memory", visibility = 0 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdPayload = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var memoryId = createdPayload.RootElement.GetProperty("memoryId").GetGuid();

        using var list = await Client.GetAsync($"/api/mobile/trips/{trip.TripPlanId}/memories");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listPayload = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Single(listPayload.RootElement.EnumerateArray());
        using var detail = await Client.GetAsync($"/api/mobile/trips/{trip.TripPlanId}/memories/{memoryId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var delete = await Client.DeleteAsync($"/api/mobile/trips/{trip.TripPlanId}/memories/{memoryId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var afterDelete = await Client.GetAsync($"/api/mobile/trips/{trip.TripPlanId}/memories/{memoryId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);

        await using var db = CreateVerificationContext();
        var tombstone = await db.TripMemories.SingleAsync(row => row.MemoryId == memoryId);
        Assert.NotNull(tombstone.DeletedAtUtc);
        Assert.Equal(trip.TripPlanId, tombstone.TripPlanId);
        Assert.Equal(trip.Stops[0].StopId, tombstone.TripStopId);
    }

    [SqlE2EFact]
    public async Task M3_AuthenticatedSessionCheckInAndFinishPersistInSql()
    {
        await RegisterAndLoginAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var create = await Client.PostAsJsonAsync("/api/mobile/trip-plans", new CreateTripPlanDto(
            "M3 HTTP SQL trip", today.AddDays(1), today.AddDays(3), 25000m, "Car", 2));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var trip = await create.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(trip);

        using var started = await Client.PostAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions", null);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        using var startedJson = JsonDocument.Parse(await started.Content.ReadAsStringAsync());
        var sessionId = startedJson.RootElement.GetProperty("sessionId").GetGuid();

        using var recovered = await Client.GetAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/recover");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);

        using var ownerToken = await Client.GetAsync("/api/mobile/identity/me");
        Assert.Equal(HttpStatusCode.OK, ownerToken.StatusCode);
        var ownerBearer = Client.DefaultRequestHeaders.Authorization!.Parameter!;

        var guestEmail = $"sql-e2e-guest-{Guid.NewGuid():N}@wayhunter.invalid";
        var guestBearer = await RegisterAndLoginAsync(guestEmail);
        await using (var lookup = CreateVerificationContext())
        {
            var guestUserId = await lookup.Users.Where(x => x.NormalizedEmail == guestEmail.ToUpperInvariant())
                .Select(x => x.UserId).SingleAsync();
            var acceptedInvitation = TripInvitation.Create(
                Guid.NewGuid(), trip.TripPlanId,
                await lookup.Users.Where(x => x.NormalizedEmail == "SQL-E2E@WAYHUNTER.INVALID").Select(x => x.UserId).SingleAsync(),
                guestUserId, DateTimeOffset.UtcNow.AddMinutes(-1));
            acceptedInvitation.Accept(DateTimeOffset.UtcNow);
            lookup.TripInvitations.Add(acceptedInvitation);
            lookup.TripMembers.Add(TripMember.FromAcceptedInvitation(
                Guid.NewGuid(), acceptedInvitation));
            await lookup.SaveChangesAsync();

            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestBearer);
            using var consent = await Client.PutAsJsonAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/gps-consent", new { isGranted = true });
            Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
            using var memberCheckIn = await Client.PostAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/im-safe", null);
            Assert.Equal(HttpStatusCode.OK, memberCheckIn.StatusCode);

            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerBearer);
            using var removal = await Client.DeleteAsync($"/api/mobile/trips/{trip.TripPlanId}/members/{guestUserId}");
            Assert.Equal(HttpStatusCode.OK, removal.StatusCode);
            await using var membershipCheck = CreateVerificationContext();
            Assert.False(await membershipCheck.TripMembers.Where(x => x.TripPlanId == trip.TripPlanId && x.MemberUserId == guestUserId).Select(x => x.IsActive).SingleAsync());
            Assert.True(await membershipCheck.TripGpsConsents.Where(x => x.SessionId == sessionId && x.UserId == guestUserId).Select(x => x.IsGranted).SingleAsync());
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestBearer);
            using var revokedMemberConsent = await Client.PutAsJsonAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/gps-consent", new { isGranted = true });
            Assert.Equal(HttpStatusCode.Forbidden, revokedMemberConsent.StatusCode);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerBearer);
        }

        using var paused = await Client.PostAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/pause", null);
        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
        using var resumed = await Client.PostAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/resume", null);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);

        using var safe = await Client.PostAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/im-safe", null);
        Assert.Equal(HttpStatusCode.OK, safe.StatusCode);
        using var safeRetry = await Client.PostAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/im-safe", null);
        Assert.Equal(HttpStatusCode.OK, safeRetry.StatusCode);
        using var retryJson = JsonDocument.Parse(await safeRetry.Content.ReadAsStringAsync());
        Assert.False(retryJson.RootElement.GetProperty("created").GetBoolean());

        using var finished = await Client.PostAsync($"/api/mobile/trips/{trip.TripPlanId}/sessions/{sessionId}/finish", null);
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);
        await using var db = CreateVerificationContext();
        Assert.Equal(1, await db.LiveTripSessions.CountAsync(x => x.SessionId == sessionId && x.Status == Wayhunter.Domain.Modules.TripSafety.Enums.LiveTripStatus.Finished));
        Assert.Equal(2, await db.TripSafeCheckIns.CountAsync(x => x.SessionId == sessionId));
        Assert.Equal(2, await db.TripSafeCheckInOutbox.CountAsync(x => db.TripSafeCheckIns.Any(c => c.SessionId == sessionId && c.CheckInId == x.CheckInId)));
    }

    [SqlE2EFact]
    public async Task M4_AuthenticatedHunterStatusAndCaptureSessionPersistInSql()
    {
        await RegisterAndLoginAsync();
        await using var db = CreateVerificationContext();
        var user = await db.Users.SingleAsync(x => x.NormalizedEmail == "sql-e2e@wayhunter.invalid");
        var submission = new HunterSubmission
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            UserNameSnapshot = user.DisplayName,
            PlaceName = "SQL Hunter draft",
            Description = "Pending evidence capture",
            District = "Jaffna",
            Latitude = 9.66845,
            Longitude = 80.0074,
            Status = HunterSubmissionStatus.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.HunterSubmissions.Add(submission);
        await db.SaveChangesAsync();

        using var status = await Client.GetAsync($"/api/mobile/hunter/submissions/{submission.Id}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using (var statusJson = JsonDocument.Parse(await status.Content.ReadAsStringAsync()))
        {
            Assert.Equal("SQL Hunter draft", statusJson.RootElement.GetProperty("placeName").GetString());
        }
        using var capture = await Client.PostAsync($"/api/mobile/hunter/submissions/{submission.Id}/capture-sessions", null);
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        var sessionDto = await capture.Content.ReadFromJsonAsync<Wayhunter.Application.Modules.HunterPlaces.Dtos.HunterStartCaptureSessionResponseDto>();
        Assert.NotNull(sessionDto);

        await using var verify = CreateVerificationContext();
        var persisted = await verify.HunterCaptureSessions.SingleAsync(x => x.Id == sessionDto.CaptureSessionId);
        Assert.Equal(user.UserId, persisted.OwnerUserId);
        Assert.False(persisted.IsConsumed);
        Assert.False(persisted.IsRevoked);
        Assert.Equal(TimeSpan.FromMinutes(10), persisted.ExpiresAtUtc - persisted.IssuedAtUtc);
        var persistedSubmission = await verify.HunterSubmissions
            .AsNoTracking().SingleAsync(x => x.Id == submission.Id);
        Assert.Equal(9.66845, persistedSubmission.Latitude);
        Assert.Equal(80.0074, persistedSubmission.Longitude);

        // This seeded record verifies SQL metadata mapping only. The test does not
        // assert that its private reference is an uploaded or verified media asset.
        var evidence = new HunterEvidence
        {
            Id = Guid.NewGuid(),
            HunterSubmissionId = submission.Id,
            EvidenceType = Wayhunter.Domain.Modules.HunterPlaces.Enums.HunterEvidenceType.LivePhoto,
            PrivateMediaReference = "test-private-reference/e2e",
            CapturedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UploadedAtUtc = DateTimeOffset.UtcNow,
            CaptureLatitude = 9.66846,
            CaptureLongitude = 80.00741,
            DurationSeconds = null
        };
        await using (var evidenceWrite = CreateVerificationContext())
        {
            evidenceWrite.HunterEvidence.Add(evidence);
            await evidenceWrite.SaveChangesAsync();
        }
        await using (var evidenceRead = CreateVerificationContext())
        {
            var storedEvidence = await evidenceRead.HunterEvidence
                .AsNoTracking().SingleAsync(x => x.Id == evidence.Id);
            Assert.Equal(evidence.CaptureLatitude, storedEvidence.CaptureLatitude);
            Assert.Equal(evidence.CaptureLongitude, storedEvidence.CaptureLongitude);
            Assert.Equal(evidence.CapturedAtUtc, storedEvidence.CapturedAtUtc);
            Assert.Equal(evidence.UploadedAtUtc, storedEvidence.UploadedAtUtc);
        }
    }

    [SqlE2EFact]
    public async Task M5M6M7_AuthenticatedReadsAndGameAttemptUseSql()
    {
        using var anonymous = await Client.GetAsync("/api/mobile/games");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var anonymousStart = await Client.PostAsync($"/api/mobile/games/{Guid.NewGuid()}/attempts", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStart.StatusCode);
        await RegisterAndLoginAsync();

        using var games = await Client.GetAsync("/api/mobile/games");
        Assert.Equal(HttpStatusCode.OK, games.StatusCode);
        var creator = UserAccount.RegisterPending("SQL E2E creator",
            $"creator-{Guid.NewGuid():N}@wayhunter.invalid", "test-only-hash", DateTimeOffset.UtcNow.AddMinutes(-2));
        creator.ActivateAfterVerifiedOtp(DateTimeOffset.UtcNow.AddMinutes(-2));
        var game = Game.CreateDraft(Guid.NewGuid(), creator.UserId, "HTTP SQL E2E game", "Fixture game", 60);
        var step = new GameStep(Guid.NewGuid(), game.Id, 1);
        var hint = new GameHint(Guid.NewGuid(), step.Id, "Find the first checkpoint", null);
        game.ReplaceDraftStages(creator.UserId, new[] { step }, new[] { hint });
        game.SubmitForAdminReview(creator.UserId, DateTimeOffset.UtcNow.AddMinutes(-1));
        game.ApproveCreation(DateTimeOffset.UtcNow);
        await using (var db = CreateVerificationContext())
        {
            db.Users.Add(creator);
            await db.SaveChangesAsync();
            await new SqlGameRepository(db).AddAsync(game);
        }

        using var detail = await Client.GetAsync($"/api/mobile/games/{game.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var start = await Client.PostAsync($"/api/mobile/games/{game.Id}/attempts", null);
        Assert.True(start.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await start.Content.ReadAsStringAsync());
        using var firstAttempt = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
        var attemptId = firstAttempt.RootElement.GetProperty("attemptId").GetGuid();
        using var retry = await Client.PostAsync($"/api/mobile/games/{game.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var resumedAttempt = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal(attemptId, resumedAttempt.RootElement.GetProperty("attemptId").GetGuid());
        await using (var db = CreateVerificationContext())
        {
            Assert.Equal(1, await db.GameAttempts.CountAsync(x => x.GameId == game.Id));
            Assert.Equal(1, await db.GameAttemptProgress.CountAsync(x => x.GameId == game.Id));
        }

        await using (var db = CreateVerificationContext())
        {
            var userId = await db.Users.Where(x => x.NormalizedEmail == "sql-e2e@wayhunter.invalid")
                .Select(x => x.UserId).SingleAsync();
            db.CoinWallets.Add(new CoinWalletRow
            {
                Id = Guid.NewGuid(), UserId = userId, PostedCoins = 175,
                ReservedCoins = 25, LifetimeEarnedCoins = 200
            });
            await db.SaveChangesAsync();
        }
        using var wallet = await Client.GetAsync("/api/mobile/coins-wallet/me");
        Assert.Equal(HttpStatusCode.OK, wallet.StatusCode);
        using var balance = JsonDocument.Parse(await wallet.Content.ReadAsStringAsync());
        Assert.Equal(175, balance.RootElement.GetProperty("postedCoins").GetInt64());
        Assert.Equal(25, balance.RootElement.GetProperty("reservedCoins").GetInt64());
        using var transactions = await Client.GetAsync("/api/mobile/coins-wallet/me/transactions");
        Assert.Equal(HttpStatusCode.OK, transactions.StatusCode);
        var now = DateTimeOffset.UtcNow;
        await using (var db = CreateVerificationContext())
        {
            var home = new Advertisement(Guid.NewGuid(), "SQL active home", "https://example.invalid/home.png", null,
                now.AddMinutes(-1), now.AddHours(1));
            home.SetPlacement(AdvertisementPlacement.HomeBanner);
            home.Enable();
            var poi = new Advertisement(Guid.NewGuid(), "SQL active POI", "https://example.invalid/poi.png", null,
                now.AddMinutes(-1), now.AddHours(1));
            poi.SetPlacement(AdvertisementPlacement.SponsoredPoi);
            poi.Enable();
            var future = new Advertisement(Guid.NewGuid(), "SQL future", "https://example.invalid/future.png", null,
                now.AddHours(1), now.AddHours(2));
            future.Enable();
            var inactive = new Advertisement(Guid.NewGuid(), "SQL draft", "https://example.invalid/draft.png", null,
                now.AddMinutes(-1), now.AddHours(1));
            db.Advertisements.AddRange(home, poi, future, inactive);
            await db.SaveChangesAsync();
        }
        using var advertisements = await Client.GetAsync("/api/mobile/advertisements/active?placement=HomeBanner");
        Assert.Equal(HttpStatusCode.OK, advertisements.StatusCode);
        using var adPayload = JsonDocument.Parse(await advertisements.Content.ReadAsStringAsync());
        Assert.Single(adPayload.RootElement.EnumerateArray(), item => item.GetProperty("title").GetString() == "SQL active home");
        using var poiAds = await Client.GetAsync("/api/mobile/advertisements/active?placement=SponsoredPoi");
        Assert.Equal(HttpStatusCode.OK, poiAds.StatusCode);
        using var poiPayload = JsonDocument.Parse(await poiAds.Content.ReadAsStringAsync());
        Assert.Single(poiPayload.RootElement.EnumerateArray(), item => item.GetProperty("title").GetString() == "SQL active POI");
    }

    private WayhunterSharedDbContext CreateVerificationContext() => new(
        new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(ConnectionString).Options);

    private sealed class CapturingOtpSender(Action<string> capture) : IIdentityOtpEmailSender
    {
        public Task<bool> SendAsync(string recipientEmail, OtpEmailContent content, CancellationToken cancellationToken = default)
        {
            var text = string.Join(" ", content.GetType().GetProperties().Select(p => p.GetValue(content)?.ToString()));
            var code = System.Text.RegularExpressions.Regex.Match(text, @"(?<!\d)\d{6}(?!\d)").Value;
            if (code.Length != 6) return Task.FromResult(false);
            capture(code);
            return Task.FromResult(true);
        }
    }
}

public sealed class SqlE2EFactAttribute : FactAttribute
{
    public SqlE2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_SQL_E2E") != "1")
            Skip = "Set WAYHUNTER_SQL_E2E=1 to run LocalDB HTTP-to-SQL acceptance tests.";
    }
}
