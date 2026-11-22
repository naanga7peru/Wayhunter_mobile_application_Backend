using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3FinalBackendFlowTests
{
    [Fact]
    public async Task InvitationToFinishRequiresExplicitConsentAndRejectsRevokedListeners()
    {
        var elapsed = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        var owner = Guid.NewGuid().ToString("D");
        var invitee = Guid.NewGuid().ToString("D");
        var stranger = Guid.NewGuid().ToString("D");
        var peer = Guid.NewGuid().ToString("D");
        using var host = new Host(owner, invitee, peer);
        using var ownerClient = Client(host, owner);
        using var memberClient = Client(host, invitee);
        using var peerClient = Client(host, peer);
        using var outsiderClient = Client(host, stranger);
        var now = DateTimeOffset.UtcNow;
        var start = DateOnly.FromDateTime(now.UtcDateTime);
        var trip = new TripPlan(owner, "Flow", start, start.AddDays(1), 100m, "Car", 3);
        var members = host.Services.GetRequiredService<InMemoryTripMembershipRevocationRepository>();
        var sessions = host.Services.GetRequiredService<ILiveTripSessionRepository>();
        var consent = host.Services.GetRequiredService<InMemoryTripSharingConsentRepository>();
        await members.AddTripAsync(trip, ct);

        var tripPath = $"/api/mobile/trips/{trip.Id:D}";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.PostAsJsonAsync(
            $"{tripPath}/invitations", new CreateTripInvitationRequest(invitee), ct)).StatusCode);
        using var invitationResponse = await ownerClient.PostAsJsonAsync(
            $"{tripPath}/invitations", new CreateTripInvitationRequest(invitee), ct);
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        var invitation = (await invitationResponse.Content.ReadFromJsonAsync<TripInvitationApiResponse>(ct))!;
        var acceptPath = $"/api/mobile/trip-invitations/{invitation.InvitationId:D}/accept";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.PostAsync(acceptPath, null, ct)).StatusCode);
        using var accepted = await memberClient.PostAsync(acceptPath, null, ct);
        accepted.EnsureSuccessStatusCode();
        var decisions = host.Services.GetRequiredService<ITripInvitationDecisionRepository>();
        var member = await decisions.GetMemberByInvitationIdAsync(invitation.InvitationId, ct);
        Assert.NotNull(member);

        // In-memory mode has separate stores. Mirror committed facts in the test only.
        await members.AddMemberAsync(member, ct);
        consent.SeedAcceptedMemberForTests(member);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsync($"{tripPath}/sessions", null, ct)).StatusCode);
        using var started = await ownerClient.PostAsync($"{tripPath}/sessions", null, ct);
        started.EnsureSuccessStatusCode();
        var sessionId = (await started.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("sessionId").GetGuid();
        var session = (await sessions.GetByIdAsync(sessionId, ct))!;
        consent.SeedSessionForTests(session);

        using var peerInvitationResponse = await ownerClient.PostAsJsonAsync(
            $"{tripPath}/invitations", new CreateTripInvitationRequest(peer), ct);
        Assert.Equal(HttpStatusCode.Created, peerInvitationResponse.StatusCode);
        var peerInvitation = (await peerInvitationResponse.Content.ReadFromJsonAsync<TripInvitationApiResponse>(ct))!;
        using var peerAccepted = await peerClient.PostAsync($"/api/mobile/trip-invitations/{peerInvitation.InvitationId:D}/accept", null, ct);
        peerAccepted.EnsureSuccessStatusCode();
        var peerMember = (await decisions.GetMemberByInvitationIdAsync(peerInvitation.InvitationId, ct))!;
        await members.AddMemberAsync(peerMember, ct);
        consent.SeedAcceptedMemberForTests(peerMember);

        var consentPath = $"{tripPath}/sessions/{sessionId:D}/gps-consent";
        using var memberSocket = await ConnectAsync(host, invitee, ct);
        using var peerSocket = await ConnectAsync(host, peer, ct);
        using var outsiderSocket = await ConnectAsync(host, stranger, ct);
        object[] join = [trip.Id, sessionId];
        Assert.True((await InvokeAsync(memberSocket, "1", "JoinTrip", join, ct, elapsed)).TryGetProperty("error", out _));
        Assert.True((await InvokeAsync(outsiderSocket, "1", "JoinTrip", join, ct)).TryGetProperty("error", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.PutAsJsonAsync(consentPath, new { isGranted = true }, ct)).StatusCode);

        (await peerClient.PutAsJsonAsync(consentPath, new { isGranted = true }, ct)).EnsureSuccessStatusCode();
        (await memberClient.PutAsJsonAsync(consentPath, new { isGranted = true }, ct)).EnsureSuccessStatusCode();
        Assert.False((await InvokeAsync(memberSocket, "2", "JoinTrip", join, ct)).TryGetProperty("error", out _));
        Assert.False((await InvokeAsync(peerSocket, "2", "JoinTrip", join, ct)).TryGetProperty("error", out _));

        object[] location = [trip.Id, sessionId, new { latitude = 9d, longitude = 80d, accuracyMeters = 10d }];
        Assert.False((await InvokeAsync(peerSocket, "3", "PublishLocation", location, ct)).TryGetProperty("error", out _));
        var update = await ReceiveAsync(memberSocket, item => item.TryGetProperty("target", out var target) &&
            target.GetString() == "TripLocationUpdated", ct);
        Assert.Equal(peer, update.GetProperty("arguments")[0].GetProperty("userId").GetString());

        (await memberClient.PutAsJsonAsync(consentPath, new { isGranted = false }, ct)).EnsureSuccessStatusCode();
        using var reconnect = await ConnectAsync(host, invitee, ct);
        Assert.True((await InvokeAsync(reconnect, "1", "JoinTrip", join, ct)).TryGetProperty("error", out _));
        Assert.True((await InvokeAsync(memberSocket, "3", "PublishLocation", location, ct)).TryGetProperty("error", out _));

        (await memberClient.PutAsJsonAsync(consentPath, new { isGranted = true }, ct)).EnsureSuccessStatusCode();
        Assert.False((await InvokeAsync(reconnect, "2", "JoinTrip", join, ct)).TryGetProperty("error", out _));
        (await ownerClient.DeleteAsync($"{tripPath}/members/{invitee}", ct)).EnsureSuccessStatusCode();
        Assert.True((await InvokeAsync(reconnect, "3", "JoinTrip", join, ct)).TryGetProperty("error", out _));

        (await ownerClient.PostAsync($"{tripPath}/sessions/{sessionId:D}/finish", null, ct)).EnsureSuccessStatusCode();
        Assert.Equal(LiveTripStatus.Finished, (await sessions.GetByIdAsync(sessionId, ct))!.Status);
        Assert.True((await InvokeAsync(peerSocket, "4", "JoinTrip", join, ct)).TryGetProperty("error", out _));
    }

    private static HttpClient Client(Host host, string user)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", user);
        return client;
    }

    private static async Task<WebSocket> ConnectAsync(Host host, string user, CancellationToken ct)
    {
        var client = host.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers["X-Test-User"] = user;
        var socket = await client.ConnectAsync(new Uri("ws://localhost/hubs/trip-location"), ct);
        await SendAsync(socket, new { protocol = "json", version = 1 }, ct);
        await ReceiveAsync(socket, item => item.ValueKind == JsonValueKind.Object &&
            !item.EnumerateObject().Any(), ct);
        return socket;
    }

    private static Task SendAsync(WebSocket socket, object message, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + '\u001e')),
            WebSocketMessageType.Text, true, ct);

    private static async Task<JsonElement> InvokeAsync(WebSocket socket, string id, string target, object[] arguments, CancellationToken ct, Stopwatch? elapsed = null)
    {
        await SendAsync(socket, new { type = 1, invocationId = id, target, arguments }, ct);
        return await ReceiveAsync(socket, item => item.TryGetProperty("invocationId", out var invocation) &&
            invocation.GetString() == id, ct, elapsed);
    }

    private static async Task<JsonElement> ReceiveAsync(WebSocket socket, Func<JsonElement, bool> match, CancellationToken ct, Stopwatch? elapsed = null)
    {
        var buffer = new byte[4096];
        var observed = new List<string>();
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                try
                {
                    received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Timed out waiting for a SignalR frame after {elapsed?.Elapsed}. Token canceled: {ct.IsCancellationRequested}. Observed: {string.Join(" | ", observed)}");
                }
                Assert.Equal(WebSocketMessageType.Text, received.MessageType);
                message.Write(buffer, 0, received.Count);
            } while (!received.EndOfMessage);

            foreach (var frame in Encoding.UTF8.GetString(message.ToArray()).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                observed.Add(frame);
                using var json = JsonDocument.Parse(frame);
                if (match(json.RootElement))
                    return json.RootElement.Clone();
            }
        }
    }

    private sealed class Host(string owner, string friend, string peer) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging =>
            {
                // Windows EventLog writes can fail in the test runner when it
                // lacks source permissions. A logging failure inside SignalR
                // would otherwise prevent the invocation completion frame.
                logging.ClearProviders();
                logging.AddConsole();
                // Keep warnings and exceptions visible without verbose request
                // diagnostics that could expose test identity or payload data.
                logging.SetMinimumLevel(LogLevel.Warning);
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("M3Flow").AddScheme<AuthenticationSchemeOptions, TestAuth>("M3Flow", _ => { });
                services.RemoveAll<ITripInviteEligibilityAccess>();
                services.AddSingleton<ITripInviteEligibilityAccess>(new Friends(owner, friend, peer));
                services.RemoveAll<ITripLiveLocationAccessReader>();
                services.AddSingleton<ITripLiveLocationAccessReader, TestAccess>();
            });
        }
    }

    private sealed class Friends(string owner, string friend, string peer) : ITripInviteEligibilityAccess
    {
        public Task<TripInviteRecipientEligibility> CheckAsync(string senderUserId, string recipientUserId, CancellationToken cancellationToken) =>
            Task.FromResult(new TripInviteRecipientEligibility(senderUserId == owner && (recipientUserId == friend || recipientUserId == peer), false));
    }

    private sealed class TestAccess(ILiveTripSessionRepository sessions, ITripMembershipRevocationRepository membership,
        InMemoryTripSharingConsentRepository consent) : ITripLiveLocationAccessReader
    {
        public async Task EnsureAllowedAsync(Guid tripPlanId, Guid sessionId, string authenticatedUserId, CancellationToken cancellationToken)
        {
            ActiveSessionLocationEventValidator.EnsureCanPublish(
                await sessions.GetByIdAsync(sessionId, cancellationToken), tripPlanId);
            var member = await membership.GetMemberAsync(tripPlanId, authenticatedUserId, cancellationToken);
            if (member?.IsActive != true)
                throw new UnauthorizedAccessException();
            await consent.EnsureAllowedAsync(tripPlanId, sessionId, authenticatedUserId, cancellationToken);
        }
    }

    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var user = Request.Headers["X-Test-User"].ToString();
            if (!Guid.TryParseExact(user, "D", out _))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", user)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
