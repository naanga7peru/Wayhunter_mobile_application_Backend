using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.TripSafety;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3Ms08NotificationHttpTests
{
    private static readonly string Owner = Guid.NewGuid().ToString("D");
    private static readonly string Other = Guid.NewGuid().ToString("D");
    private static readonly Guid Event = Guid.NewGuid();

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("conflicting", HttpStatusCode.Unauthorized)]
    [InlineData("member", HttpStatusCode.ServiceUnavailable)]
    public async Task DefaultInboxRejectsUntrustedIdentityOrFailsClosed(string identity, HttpStatusCode status)
    {
        await using var app = await CreateHostAsync();
        using var client = Client(app, identity);
        using var inbox = await client.GetAsync("/api/mobile/notifications");
        Assert.Equal(status, inbox.StatusCode);
        Assert.IsType<UnavailableNotificationPushProvider>(app.Services.GetRequiredService<INotificationPushProvider>());
        Assert.DoesNotContain(app.Services.GetServices<IHostedService>(), x => x is NotificationDispatchWorker);
    }

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("member", HttpStatusCode.Forbidden)]
    [InlineData("conflicting", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.ServiceUnavailable)]
    public async Task ComposerRequiresTrustedAdminAndUnavailableScopeDoesNotQueue(string identity, HttpStatusCode status)
    {
        await using var app = await CreateHostAsync();
        using var client = Client(app, identity);
        var command = new NotificationAdminCommand(Guid.NewGuid(), NotificationTemplate.CoinsUpdate, null,
            new(null, null, [Owner]), null, null, null);
        using var response = await client.PostAsJsonAsync("/api/admin/notifications/preview", command);
        Assert.Equal(status, response.StatusCode);
    }

    [Fact]
    public async Task InboxAndReadMarkersArePrivateToAuthenticatedRecipient()
    {
        await using var app = await CreateHostAsync(readyInbox: true);
        using var owner = Client(app, "member");
        using var other = Client(app, "other");
        var mine = await owner.GetFromJsonAsync<NotificationInboxPage>("/api/mobile/notifications");
        var theirs = await other.GetFromJsonAsync<NotificationInboxPage>("/api/mobile/notifications");

        Assert.Equal(Event, Assert.Single(mine!.Items).EventId);
        Assert.Equal(1, mine.UnreadCount);
        Assert.Empty(theirs!.Items);
        using var denied = await other.PutAsync($"/api/mobile/notifications/{Event:D}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var marked = await owner.PutAsync($"/api/mobile/notifications/{Event:D}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal(0, (await owner.GetFromJsonAsync<NotificationInboxPage>("/api/mobile/notifications"))!.UnreadCount);
    }

    [Fact]
    public async Task DeviceMutationValidatesInputWithoutEchoingTokenAndDefaultRegistryIsUnavailable()
    {
        await using var app = await CreateHostAsync();
        using var client = Client(app, "member");
        var route = $"/api/mobile/notifications/devices/{Guid.NewGuid():D}";
        const string invalid = "private test token with spaces";
        using var rejected = await client.PutAsJsonAsync(route, new NotificationDeviceRequest(Guid.NewGuid(), invalid, true));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.DoesNotContain(invalid, await rejected.Content.ReadAsStringAsync());
        using var unavailable = await client.PutAsJsonAsync(route, new NotificationDeviceRequest(Guid.NewGuid(), new string('A', 50), true));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
    }

    private static async Task<WebApplication> CreateHostAsync(bool readyInbox = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Identity:AdministratorRole"] = "Admin";
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Handler>("Test", _ => { });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ITripPlanRepository, InMemoryTripPlanRepository>();
        builder.Services.AddTripSafetyBatch2Services(builder.Configuration);
        builder.Services.AddNotificationFoundation(builder.Configuration);
        if (readyInbox)
        {
            builder.Services.RemoveAll<INotificationInbox>();
            builder.Services.AddSingleton<INotificationInbox, TestInbox>();
        }
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app, string identity)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
        return client;
    }

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var kind = Request.Headers["X-Test-Identity"].ToString();
            if (kind == "anonymous")
                return Task.FromResult(AuthenticateResult.NoResult());
            var user = kind == "other" ? Other : Owner;
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user),
                new Claim("sub", kind == "conflicting" ? Other : user),
                new Claim(ClaimTypes.Role, kind is "admin" or "conflicting" ? "Admin" : "Member")
            };
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private sealed class TestInbox : INotificationInbox
    {
        private DateTimeOffset? _readAt;
        public Task<NotificationInboxPage> ReadAsync(string recipientId, int limit, CancellationToken ct) =>
            Task.FromResult<NotificationInboxPage>(recipientId == Owner
                ? new([new(Event, NotificationCategory.Coins, NotificationTemplate.CoinsUpdate, 1, null,
                    DateTimeOffset.UtcNow, _readAt)], _readAt.HasValue ? 0 : 1)
                : new([], 0));
        public Task<bool> MarkReadAsync(string recipientId, Guid eventId, DateTimeOffset now, CancellationToken ct)
        {
            if (recipientId != Owner || eventId != Event)
                return Task.FromResult(false);
            _readAt ??= now;
            return Task.FromResult(true);
        }
        public Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(string recipientId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NotificationPreference>>([]);
        public Task SetPreferenceAsync(string recipientId, NotificationPreference preference, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
