using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Integrations.Maps;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed partial class LocalDbAuthenticationPhase3Tests
{
    [LocalDbM3FunctionalFact]
    public Task VerifyM3Operations()
    {
        var catalog = Environment.GetEnvironmentVariable("WAYHUNTER_M3_FUNCTIONAL_CATALOG");
        Assert.True(catalog is not null && Regex.IsMatch(catalog, @"\AWayhunter_M3_Functional_[a-f0-9]{32}\z"));
        var simulate = Environment.GetEnvironmentVariable("WAYHUNTER_M3_SIMULATE_PROVIDERS") == "1";
        WebSocketClient? socketClient = null;
        return RunIsolatedAsync(catalog!, migrateFreshCatalog: true,
            (client, db, capture, clock, _) =>
            {
                clock.Offset = TimeSpan.Zero;
                return VerifyTripSafetyAsync(client, db, capture, socketClient!, simulate);
            },
            services =>
            {
                services.RemoveAll<GoogleRoutesProvider>();
                services.RemoveAll<IRoutesProvider>();
                services.AddSingleton<IRoutesProvider, TripSafetyRouteStub>();
                if (simulate)
                    AddSimulatedTripSafetyProviders(services);
            },
            new Dictionary<string, string?>
            {
                ["TripSafety:EnableSqlPersistence"] = "true",
                ["TripSafety:EnableSafeCheckIn"] = "true",
                ["TripSafety:EnableSqlTripMemories"] = "true"
            },
            sockets => socketClient = sockets);
    }

    private async Task VerifyTripSafetyAsync(HttpClient client, WayhunterSharedDbContext db, Capture capture,
        WebSocketClient sockets, bool simulate)
    {
        var owner = await RegisterAsync("Owner");
        var member = await RegisterAsync("Member");
        var other = await RegisterAsync("Other");
        var admin = await RegisterAsync("Administrator");
        db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(admin.Id, DateTimeOffset.UtcNow, null));
        await db.SaveChangesAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        Use(owner.Token);
        using var planResponse = await client.PostAsJsonAsync("/api/mobile/trip-plans",
            new CreateTripPlanDto("Trip Safety functional fixture", date, date.AddDays(1), 1000, "Car", 3)
            {
                InitialStops = [new("A", "test-a", 9.66, 80.02), new("B", "test-b", 9.67, 80.03)]
            });
        Assert.Equal(201, (int)planResponse.StatusCode);
        var tripId = (await planResponse.Content.ReadFromJsonAsync<TripPlanResponseDto>())!.TripPlanId;
        var trip = $"/api/mobile/trips/{tripId}";
        const string tripRoute = "/api/mobile/trips/{tripPlanId}";
        const string invitation = "/api/mobile/trip-invitations";
        const string invitationRoute = invitation + "/{invitationId}";
        const string notifications = "/api/mobile/notifications";
        const string adminNotifications = "/api/admin/notifications";
        const string adminHazard = "/api/admin/hazards/{hazardId}";
        const string monitoring = "/api/admin/safety/trips/{tripPlanId}/sessions/{sessionId}";
        var placeholder = Guid.NewGuid();
        var routes = new Dictionary<(string Method, string Route), bool>();
        async Task<JsonElement> Check(string method, string route, int expected, string? path = null, object? body = null)
        {
            var resolved = path ?? Regex.Replace(route, @"\{[^}]+\}", placeholder.ToString("D"));
            using var request = new HttpRequestMessage(new HttpMethod(method), resolved);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            var key = (method, route);
            routes[key] = routes.GetValueOrDefault(key, true) && expected == (int)response.StatusCode;
            output.WriteLine($"{method} {route}: expected {expected}, actual {(int)response.StatusCode}.");
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType is "application/json")
                return await response.Content.ReadFromJsonAsync<JsonElement>();
            return default;
        }
        var operations = new (string Method, string Route)[]
        {
            ("POST", adminHazard + "/resolve"), ("POST", adminHazard + "/verify"),
            ("POST", adminNotifications + "/preview"), ("POST", adminNotifications + "/schedules"),
            ("POST", adminNotifications + "/schedules/{jobId}/cancel"), ("GET", adminNotifications + "/schedules/{jobId}/summary"),
            ("GET", monitoring), ("GET", notifications), ("PUT", notifications + "/{eventId}/read"),
            ("PUT", notifications + "/devices/{deviceId}"), ("POST", notifications + "/devices/{deviceId}/revoke"),
            ("GET", notifications + "/preferences"), ("PUT", notifications + "/preferences"),
            ("PUT", notifications + "/reminders"), ("POST", notifications + "/reminders/{jobId}/cancel"),
            ("POST", invitationRoute + "/accept"), ("POST", invitationRoute + "/cancel"),
            ("POST", invitationRoute + "/reject"), ("GET", invitation + "/pending"),
            ("GET", tripRoute + "/hazards"), ("POST", tripRoute + "/hazards"),
            ("POST", tripRoute + "/invitations"), ("DELETE", tripRoute + "/members/{memberUserId}"),
            ("GET", tripRoute + "/memories"), ("POST", tripRoute + "/memories"),
            ("DELETE", tripRoute + "/memories/{memoryId}"), ("GET", tripRoute + "/memories/{memoryId}"),
            ("POST", tripRoute + "/memories/{memoryId}/media/uploads"),
            ("POST", tripRoute + "/memories/{memoryId}/media/uploads/{operationId}/complete"),
            ("POST", tripRoute + "/sessions"), ("POST", tripRoute + "/sessions/{sessionId}/finish"),
            ("PUT", tripRoute + "/sessions/{sessionId}/gps-consent"), ("POST", tripRoute + "/sessions/{sessionId}/im-safe"),
            ("POST", tripRoute + "/sessions/{sessionId}/pause"), ("GET", tripRoute + "/sessions/{sessionId}/recover"),
            ("POST", tripRoute + "/sessions/{sessionId}/resume")
        };
        using (var spec = await client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json"))
        {
            var paths = spec!.RootElement.GetProperty("paths");
            foreach (var operation in operations)
                Assert.True(paths.GetProperty(operation.Route).TryGetProperty(operation.Method.ToLowerInvariant(), out _), operation.Route);
        }
        foreach (var token in new string?[] { null, "invalid.jwt.token" })
        {
            Use(token);
            foreach (var (method, route) in operations)
                await Check(method, route, 401);
        }

        Use(other.Token);
        await Check("POST", tripRoute + "/invitations", 403, trip + "/invitations", new { recipientUserId = member.Id });
        Use(owner.Token);
        var acceptedId = await Invite(member.Id);
        var rejectedId = await Invite(other.Id);
        Use(member.Token);
        await Check("GET", invitation + "/pending", 200);
        await Check("POST", invitationRoute + "/accept", 200, $"{invitation}/{acceptedId}/accept");
        Use(other.Token);
        await Check("POST", invitationRoute + "/reject", 200, $"{invitation}/{rejectedId}/reject");
        Use(owner.Token);
        var cancelledId = await Invite(admin.Id);
        await Check("POST", invitationRoute + "/cancel", 200, $"{invitation}/{cancelledId}/cancel");
        Assert.True(await db.TripMembers.AnyAsync(x => x.TripPlanId == tripId && x.MemberUserId == member.Id && x.IsActive));
        Assert.True(await db.TripInvitations.AnyAsync(x => x.Id == rejectedId && x.Status == TripInvitationStatus.Rejected));
        Assert.True(await db.TripInvitations.AnyAsync(x => x.Id == cancelledId && x.Status == TripInvitationStatus.Cancelled));

        Use(member.Token);
        await Check("POST", tripRoute + "/sessions", 403, trip + "/sessions");
        Use(owner.Token);
        var started = await Check("POST", tripRoute + "/sessions", 200, trip + "/sessions");
        var sessionId = started.GetProperty("sessionId").GetGuid();
        var session = $"{trip}/sessions/{sessionId}";
        const string sessionRoute = tripRoute + "/sessions/{sessionId}";
        await Check("GET", sessionRoute + "/recover", 200, session + "/recover");
        await Check("POST", sessionRoute + "/pause", 200, session + "/pause");
        Assert.True(await db.LiveTripSessions.AnyAsync(x => x.SessionId == sessionId && x.Status == LiveTripStatus.Paused));
        await Check("POST", sessionRoute + "/resume", 200, session + "/resume");
        Assert.True(await db.LiveTripSessions.AnyAsync(x => x.SessionId == sessionId && x.Status == LiveTripStatus.Active));

        Use(other.Token);
        await Check("PUT", sessionRoute + "/gps-consent", 403, session + "/gps-consent", new { isGranted = true });
        await Check("POST", sessionRoute + "/im-safe", 403, session + "/im-safe");
        Use(member.Token);
        var safe = await Check("POST", sessionRoute + "/im-safe", 200, session + "/im-safe");
        var safeRetry = await Check("POST", sessionRoute + "/im-safe", 200, session + "/im-safe");
        Assert.Equal(safe.GetProperty("checkInId").GetGuid(), safeRetry.GetProperty("checkInId").GetGuid());
        Assert.Equal(1, await db.TripSafeCheckIns.CountAsync());
        Assert.Equal(1, await db.TripSafeCheckInOutbox.CountAsync());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var socket = await ConnectAsync(member.Token, timeout.Token);
        Assert.True(await HubCallHasError(socket, "1", "JoinTrip", [tripId, sessionId], timeout.Token));
        await Check("PUT", sessionRoute + "/gps-consent", 200, session + "/gps-consent", new { isGranted = true });
        Assert.False(await HubCallHasError(socket, "2", "JoinTrip", [tripId, sessionId], timeout.Token));
        Assert.False(await HubCallHasError(socket, "3", "PublishLocation",
            [tripId, sessionId, new { latitude = 9.66, longitude = 80.02, accuracyMeters = 10 }], timeout.Token));
        await Check("PUT", sessionRoute + "/gps-consent", 200, session + "/gps-consent", new { isGranted = false });
        Assert.True(await HubCallHasError(socket, "4", "PublishLocation",
            [tripId, sessionId, new { latitude = 9.66, longitude = 80.02, accuracyMeters = 10 }], timeout.Token));
        output.WriteLine("SignalR WebSocket with real JWT: handshake, deny before consent, join/publish after grant, deny after revocation PASS.");

        Use(owner.Token);
        await Check("POST", tripRoute + "/hazards", 400, trip + "/hazards",
            new { latitude = 100, longitude = 80.02, description = "Synthetic", photoAssetReference = "local-evidence", photoCapturedAtUtc = DateTimeOffset.UtcNow });
        var hazard = await Check("POST", tripRoute + "/hazards", 201, trip + "/hazards",
            new { latitude = 9.66, longitude = 80.02, description = "Synthetic road hazard", photoAssetReference = "local-evidence", photoCapturedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) });
        var hazardId = hazard.GetProperty("hazardId").GetGuid();
        var warnings = await Check("GET", tripRoute + "/hazards", 200, trip + "/hazards");
        Assert.Contains(warnings.EnumerateArray(), x => x.GetProperty("hazardId").GetGuid() == hazardId);
        Assert.False(warnings[0].TryGetProperty("photoAssetReference", out _));
        Use(other.Token);
        await Check("GET", tripRoute + "/hazards", 403, trip + "/hazards");
        await Check("POST", adminHazard + "/verify", 403, $"/api/admin/hazards/{hazardId}/verify");
        Use(admin.Token);
        await Check("POST", adminHazard + "/verify", 200, $"/api/admin/hazards/{hazardId}/verify");
        await Check("POST", adminHazard + "/resolve", 200, $"/api/admin/hazards/{hazardId}/resolve", new { reason = "Synthetic resolution" });
        Assert.True(await db.HazardReports.AnyAsync(x => x.Id == hazardId && x.Status == HazardStatus.Resolved));

        Use(owner.Token);
        var memory = await Check("POST", tripRoute + "/memories", 201, trip + "/memories", new { operationId = Guid.NewGuid(), caption = "Synthetic memory" });
        var memoryId = memory.GetProperty("memoryId").GetGuid();
        var memoryUrl = $"{trip}/memories/{memoryId}";
        await Check("GET", tripRoute + "/memories", 200, trip + "/memories");
        await Check("GET", tripRoute + "/memories/{memoryId}", 200, memoryUrl);
        Use(member.Token);
        await Check("GET", tripRoute + "/memories/{memoryId}", 404, memoryUrl);
        Use(owner.Token);
        var uploadId = Guid.NewGuid();
        await Check("POST", tripRoute + "/memories/{memoryId}/media/uploads", simulate ? 200 : 503, memoryUrl + "/media/uploads",
            new { operationId = uploadId, contentType = "image/png", contentLength = SimulatedTripMemoryMedia.PngLength });
        await Check("POST", tripRoute + "/memories/{memoryId}/media/uploads/{operationId}/complete", simulate ? 200 : 503,
            $"{memoryUrl}/media/uploads/{uploadId}/complete");
        if (simulate)
            Assert.True(await db.TripMemoryMedia.AnyAsync(x => x.OperationId == uploadId &&
                x.State == Wayhunter.Application.Modules.TripSafety.Memories.TripMemoryMediaState.Attached));
        await Check("DELETE", tripRoute + "/memories/{memoryId}", 204, memoryUrl);
        Assert.True(await db.TripMemories.AnyAsync(x => x.MemoryId == memoryId && x.DeletedAtUtc != null));

        var command = new NotificationAdminCommand(Guid.NewGuid(), NotificationTemplate.SafetyUpdate, null,
            new NotificationAudienceFilter(null, null, [member.Id]), null, null, null);
        var reminder = new NotificationReminderCommand(Guid.NewGuid(), tripId, sessionId, date, DateTimeOffset.UtcNow.AddHours(1));
        if (simulate)
        {
            await VerifySimulatedNotificationsAsync(db, Check, Use, owner.Id, owner.Token, other.Token, admin.Token, command, reminder);
            Use(owner.Token);
        }
        else
        {
            await Check("GET", notifications, 503);
            await Check("PUT", notifications + "/{eventId}/read", 503);
            await Check("GET", notifications + "/preferences", 503);
            await Check("PUT", notifications + "/preferences", 503, body: new { category = 1, pushEnabled = false });
            await Check("PUT", notifications + "/devices/{deviceId}", 503,
                body: new { operationId = Guid.NewGuid(), token = "synthetic-device-token", pushAllowed = false });
            await Check("POST", notifications + "/devices/{deviceId}/revoke", 503, body: new { operationId = Guid.NewGuid() });
            await Check("PUT", notifications + "/reminders", 503, body: reminder);
            await Check("POST", notifications + "/reminders/{jobId}/cancel", 503, body: new { operationId = Guid.NewGuid() });
        }
        var targetMonitoring = $"/api/admin/safety/trips/{tripId}/sessions/{sessionId}";
        await Check("GET", monitoring, 403, targetMonitoring);
        await Check("POST", adminNotifications + "/preview", 403, body: command);
        Use(admin.Token);
        await Check("GET", monitoring, 503, targetMonitoring);
        if (!simulate)
        {
            await Check("POST", adminNotifications + "/preview", 503, body: command);
            await Check("POST", adminNotifications + "/schedules", 503, body: new { command, confirmBulk = false });
            await Check("POST", adminNotifications + "/schedules/{jobId}/cancel", 503, body: new { operationId = Guid.NewGuid() });
            await Check("GET", adminNotifications + "/schedules/{jobId}/summary", 503);
            Assert.Empty(await db.NotificationJobs.ToListAsync());
            Assert.Empty(await db.NotificationDevices.ToListAsync());
        }

        Use(owner.Token);
        await Check("DELETE", tripRoute + "/members/{memberUserId}", 200, trip + "/members/" + member.Id);
        Assert.True(await db.TripMembers.AnyAsync(x => x.TripPlanId == tripId && x.MemberUserId == member.Id && !x.IsActive));
        await Check("POST", sessionRoute + "/finish", 200, session + "/finish");
        Assert.True(await db.LiveTripSessions.AnyAsync(x => x.SessionId == sessionId && x.Status == LiveTripStatus.Finished));
        Assert.Equal(36, routes.Count);
        Assert.True(routes.Values.All(x => x), "Inspect the sanitized route status matrix.");

        void Use(string? token) => client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

        async Task<Guid> Invite(string recipient)
        {
            var response = await Check("POST", tripRoute + "/invitations", 201, trip + "/invitations", new { recipientUserId = recipient });
            return response.GetProperty("invitationId").GetGuid();
        }

        async Task<(string Id, string Token)> RegisterAsync(string name)
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            var password = Guid.NewGuid().ToString("N") + "A!";
            using var registration = await client.PostAsJsonAsync("/api/mobile/identity/register",
                new { displayName = name, email, password, confirmPassword = password });
            Assert.Equal(201, (int)registration.StatusCode);
            using var verify = await client.PostAsJsonAsync("/api/mobile/identity/verify-otp", new { email, code = capture.Codes.Last() });
            Assert.Equal(204, (int)verify.StatusCode);
            using var login = await client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
            Assert.Equal(200, (int)login.StatusCode);
            using var result = (await login.Content.ReadFromJsonAsync<JsonDocument>())!;
            return (result.RootElement.GetProperty("userId").GetString()!, result.RootElement.GetProperty("accessToken").GetString()!);
        }

        async Task<WebSocket> ConnectAsync(string token, CancellationToken ct)
        {
            sockets.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + token;
            var connected = await sockets.ConnectAsync(new Uri("ws://localhost/hubs/trip-location"), ct);
            await SendFrame(connected, new { protocol = "json", version = 1 }, ct);
            _ = await ReadFrame(connected, frame => !frame.EnumerateObject().Any(), ct);
            return connected;
        }
    }

    private static async Task<bool> HubCallHasError(WebSocket socket, string id, string target, object[] arguments, CancellationToken ct)
    {
        await SendFrame(socket, new { type = 1, invocationId = id, target, arguments }, ct);
        return (await ReadFrame(socket, frame => frame.TryGetProperty("invocationId", out var value) &&
            value.GetString() == id, ct)).TryGetProperty("error", out _);
    }

    private static Task SendFrame(WebSocket socket, object frame, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame) + '\u001e')),
            WebSocketMessageType.Text, true, ct);

    private static async Task<JsonElement> ReadFrame(WebSocket socket, Func<JsonElement, bool> matches, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (true)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                Assert.Equal(WebSocketMessageType.Text, result.MessageType);
                stream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            foreach (var item in Encoding.UTF8.GetString(stream.ToArray()).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var json = JsonDocument.Parse(item);
                if (matches(json.RootElement)) return json.RootElement.Clone();
            }
        }
    }

    private sealed class TripSafetyRouteStub : IRoutesProvider
    {
        public Task<RouteProviderResult> CalculateRouteAsync(IReadOnlyList<RouteWaypoint> stops, CancellationToken cancellationToken) =>
            Task.FromResult(new RouteProviderResult(true, stops, 3, 10, "synthetic", null));
    }
}

public sealed class LocalDbM3FunctionalFactAttribute : FactAttribute
{
    public LocalDbM3FunctionalFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_LOCAL_M3_FUNCTIONAL") != "1")
            Skip = "Requires a fresh dedicated LocalDB catalog and explicit M3 opt-in.";
    }
}
