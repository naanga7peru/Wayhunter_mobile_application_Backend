using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Controllers.Admin.TripSafety;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3AdminSafetyHttpTests
{
    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("invalid", HttpStatusCode.Unauthorized)]
    [InlineData("revoked-session", HttpStatusCode.Unauthorized)]
    [InlineData("normal", HttpStatusCode.Forbidden)]
    [InlineData("revoked-role", HttpStatusCode.Forbidden)]
    [InlineData("unavailable-role", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.OK)]
    public async Task RequiresVerifiedServerRoleAndActiveM1Session(string identity, HttpStatusCode status)
    {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.Client(identity);
        using var response = await client.GetAsync(fixture.Route);
        Assert.Equal(status, response.StatusCode);
        if (status == HttpStatusCode.OK)
            Assert.Equal(AdminSafetyOutcome.Success, Assert.Single(fixture.Data.Audit).Outcome);
        else
            Assert.Empty(fixture.Data.Audit);
    }

    [Fact]
    public async Task MonitoringFiltersScopeMembershipConsentAndPrivateData()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.Client("admin");
        using var response = await client.GetAsync(fixture.Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal(new[] { "activeParticipantCount", "activeSeparationCount", "checkedInParticipantCount",
            "hazards", "sessionStatus", "target", "tripStatus" },
            body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(3, body.GetProperty("activeParticipantCount").GetInt32());
        Assert.Equal(1, body.GetProperty("checkedInParticipantCount").GetInt32());
        Assert.Equal(1, body.GetProperty("activeSeparationCount").GetInt32());
        var hazard = Assert.Single(body.GetProperty("hazards").EnumerateArray().ToArray());
        Assert.Equal(3, hazard.EnumerateObject().Count());
        Assert.Equal((int)HazardStatus.Verified, hazard.GetProperty("status").GetInt32());
        Assert.True(hazard.TryGetProperty("photoCapturedAtUtc", out _));
        Assert.True(hazard.TryGetProperty("expiresAtUtc", out _));
        Assert.DoesNotContain(fixture.Participant, body.GetRawText(), StringComparison.Ordinal);

        await fixture.Consent.SetMyConsentAsync(fixture.Data.Target.TripPlanId, fixture.Data.Target.SessionId,
            fixture.Participant, false, DateTimeOffset.UtcNow);
        var revoked = await client.GetFromJsonAsync<AdminSafetySummary>(fixture.Route);
        Assert.Equal(0, revoked!.ActiveSeparationCount);
        Assert.Equal(1, revoked.CheckedInParticipantCount);

        fixture.Data.Sections = AdminSafetySections.Hazards;
        var limited = await client.GetFromJsonAsync<AdminSafetySummary>(fixture.Route);
        Assert.Null(limited!.ActiveParticipantCount);
        Assert.Null(limited.CheckedInParticipantCount);
        Assert.Null(limited.ActiveSeparationCount);
        Assert.Equal(AdminSafetySections.Hazards, fixture.Data.LastReadSections);
    }

    [Fact]
    public async Task WrongScopeInvalidTargetAndUnavailableProjectionFailClosedAndAreAudited()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.Client("admin");
        using var wrong = await client.GetAsync(Fixture.Uri(fixture.Data.Target with { SessionId = fixture.OtherSession }));
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        using var denied = await client.GetAsync(Fixture.Uri(fixture.Data.Target with { TripPlanId = Guid.NewGuid() }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var invalid = await client.GetAsync(Fixture.Uri(fixture.Data.Target with { SessionId = Guid.Empty }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        fixture.Data.WrongProjection = true;
        using var unavailable = await client.GetAsync(fixture.Route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(new[] { AdminSafetyOutcome.NotFound, AdminSafetyOutcome.Denied,
            AdminSafetyOutcome.InvalidTarget, AdminSafetyOutcome.Unavailable }, fixture.Data.Audit.Select(record => record.Outcome));
        Assert.All(fixture.Data.Audit, record => Assert.Equal(fixture.AdminId, record.Actor));
    }

    [Fact]
    public async Task MissingProductionAdaptersKeepMonitoringDisabled()
    {
        await using var fixture = await Fixture.CreateAsync(registerReadAdapters: false);
        using var client = fixture.Client("admin");
        using var response = await client.GetAsync(fixture.Route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var action = await client.PostAsync(fixture.Route, null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, action.StatusCode);
    }

    [Fact]
    public async Task RepositoryHostHasNoMonitoringActivationDependencies()
    {
        using var host = new WebApplicationFactory<Program>();
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.Null(services.GetService<TrustedAdministratorRoleResolutionService>());
        Assert.Null(services.GetService<IAdminSafetyScopeReader>());
        Assert.Null(services.GetService<IAdminSafetyProjectionReader>());
        Assert.Null(services.GetService<IAdminSafetyAuditSink>());
        using var client = host.CreateClient();
        using var response = await client.GetAsync(Fixture.Uri(new(Guid.NewGuid(), Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("role", HttpStatusCode.Forbidden)]
    [InlineData("removed-grant", HttpStatusCode.Forbidden)]
    [InlineData("changed-grant", HttpStatusCode.Forbidden)]
    [InlineData("membership", HttpStatusCode.ServiceUnavailable)]
    [InlineData("consent", HttpStatusCode.ServiceUnavailable)]
    [InlineData("finished", HttpStatusCode.ServiceUnavailable)]
    [InlineData("cancelled", HttpStatusCode.ServiceUnavailable)]
    public async Task ChangesWhileAuditIsPendingWithholdThePreparedSummary(string change, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Data.OnAudit = async () =>
        {
            switch (change)
            {
                case "role": fixture.Data.RoleStatus = AdministratorRoleAssignmentStatus.Revoked; break;
                case "removed-grant": fixture.Data.GrantRemoved = true; break;
                case "changed-grant": fixture.Data.Sections = AdminSafetySections.Hazards; break;
                case "membership":
                    var member = await fixture.Members.GetMemberAsync(fixture.Trip.Id, fixture.Participant);
                    await fixture.Members.RemoveMemberAndQueueNotificationAsync(fixture.Trip.Id, member!.Id, DateTimeOffset.UtcNow);
                    break;
                case "consent":
                    await fixture.Consent.SetMyConsentAsync(fixture.Trip.Id, fixture.Session.SessionId,
                        fixture.Participant, false, DateTimeOffset.UtcNow);
                    break;
                case "finished": fixture.Session.Finish(DateTimeOffset.UtcNow); break;
                case "cancelled": fixture.Trip.Cancel(); break;
            }
        };
        using var client = fixture.Client("admin");
        using var response = await client.GetAsync(fixture.Route);
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("activeParticipantCount", await response.Content.ReadAsStringAsync());
        Assert.Equal(AdminSafetyOutcome.Success, fixture.Data.Audit[0].Outcome);
        Assert.Equal(expected == HttpStatusCode.Forbidden ? AdminSafetyOutcome.Denied : AdminSafetyOutcome.Unavailable,
            fixture.Data.Audit[1].Outcome);
        Assert.Equal(2, fixture.Data.Audit.Count);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("invalid")]
    [InlineData("oversized")]
    public async Task InvalidProjectionIsUnavailableAndAudited(string change)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Data.TransformProjection = projection => change switch
        {
            "stale" => projection with { ObservedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) },
            "future" => projection with { ObservedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1) },
            "invalid" => projection with { Hazards = [null!] },
            _ => projection with { Participants = Enumerable.Repeat(fixture.Participant, 257).ToArray() }
        };
        using var client = fixture.Client("admin");
        using var response = await client.GetAsync(fixture.Route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(AdminSafetyOutcome.Unavailable, Assert.Single(fixture.Data.Audit).Outcome);
    }

    [Fact]
    public async Task SessionChangeDuringProjectionAndAuditFailuresSuppressResponse()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Data.OnProjection = () =>
        {
            fixture.Session.Finish(DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        };
        using var client = fixture.Client("admin");
        using var response = await client.GetAsync(fixture.Route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(AdminSafetyOutcome.Unavailable, Assert.Single(fixture.Data.Audit).Outcome);

        fixture.Data.AuditUnavailable = true;
        using var failedAudit = await client.GetAsync(fixture.Route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedAudit.StatusCode);
        Assert.DoesNotContain("sessionStatus", await failedAudit.Content.ReadAsStringAsync());
        Assert.Single(fixture.Data.Audit);
    }

    private sealed class Fixture(WebApplication app, UserAccount admin, UserAccount normal,
        IdentitySession adminSession, IdentitySession normalSession, DataSource data, string participant,
        Guid otherSession, InMemoryTripSharingConsentRepository consent, TripPlan trip, LiveTripSession session, InMemoryTripMembershipRevocationRepository members) : IAsyncDisposable
    {
        public DataSource Data { get; } = data;
        public InMemoryTripSharingConsentRepository Consent { get; } = consent;
        public string Participant { get; } = participant;
        public TripPlan Trip { get; } = trip;
        public LiveTripSession Session { get; } = session;
        public InMemoryTripMembershipRevocationRepository Members { get; } = members;
        public Guid OtherSession { get; } = otherSession;
        public string AdminId => admin.UserId;
        public string Route => Uri(Data.Target);
        public static string Uri(AdminSafetyTarget target) => $"/api/admin/safety/trips/{target.TripPlanId:D}/sessions/{target.SessionId:D}";

        public HttpClient Client(string kind)
        {
            var client = app.GetTestClient();
            if (kind == "anonymous")
                return client;
            var tokenService = app.Services.GetRequiredService<SignedSessionTokenService>();
            var now = DateTimeOffset.UtcNow;
            var token = kind == "invalid" ? "invalid" :
                kind == "normal" ? tokenService.Issue(normal, normalSession, now) : tokenService.Issue(admin, adminSession, now);
            if (kind == "revoked-session")
                adminSession.Revoke(now);
            if (kind == "revoked-role")
                Data.RoleStatus = AdministratorRoleAssignmentStatus.Revoked;
            if (kind == "unavailable-role")
                Data.RoleUnavailable = true;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public static async Task<Fixture> CreateAsync(bool registerReadAdapters = true)
        {
            var now = DateTimeOffset.UtcNow;
            var admin = CreateAccount("Admin", now);
            var normal = CreateAccount("Normal", now);
            var adminSession = IdentitySession.Create(admin.UserId, now.AddMinutes(-5), now.AddHours(1));
            var normalSession = IdentitySession.Create(normal.UserId, now.AddMinutes(-5), now.AddHours(1));
            var owner = Guid.NewGuid().ToString("D");
            var participant = Guid.NewGuid().ToString("D");
            var peer = Guid.NewGuid().ToString("D");
            var outsider = Guid.NewGuid().ToString("D");
            var trip = new TripPlan(owner, "Private title", new(2026, 11, 21), new(2026, 11, 22), 0, "Car", 4);
            var session = LiveTripSession.Start(Guid.NewGuid(), trip.Id, now.AddHours(-1));
            var other = LiveTripSession.Start(Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-1));
            var target = new AdminSafetyTarget(trip.Id, session.SessionId);

            var members = new InMemoryTripMembershipRevocationRepository();
            var consent = new InMemoryTripSharingConsentRepository();
            var sessions = new InMemoryLiveTripSessionRepository();
            await members.AddTripAsync(trip);
            await sessions.AddAsync(session);
            await sessions.AddAsync(other);
            consent.SeedSessionForTests(session);
            foreach (var user in new[] { participant, peer })
            {
                var invitation = TripInvitation.Create(Guid.NewGuid(), trip.Id, owner, user, now.AddMinutes(-10));
                invitation.Accept(now.AddMinutes(-9));
                var member = TripMember.FromAcceptedInvitation(Guid.NewGuid(), invitation);
                await members.AddMemberAsync(member);
                consent.SeedAcceptedMemberForTests(member);
                await consent.SetMyConsentAsync(trip.Id, session.SessionId, user, true, now.AddMinutes(-8));
            }

            var data = new DataSource(target, new(target, now,
                [owner, participant, peer, outsider, peer],
                [new(HazardStatus.Verified, now.AddHours(-2), now.AddHours(1)),
                 new(HazardStatus.Resolved, now.AddHours(-2), now.AddHours(1)),
                 new(HazardStatus.Reported, now.AddHours(-2), now.AddTicks(-1))],
                [new(participant, peer), new(peer, participant), new(owner, peer), new(outsider, peer)],
                [new(Guid.NewGuid(), trip.Id, session.SessionId, participant, now.AddMinutes(-1)),
                 new(Guid.NewGuid(), trip.Id, session.SessionId, outsider, now.AddMinutes(-1)),
                 new(Guid.NewGuid(), other.TripPlanId, other.SessionId, peer, now.AddMinutes(-1))]),
                admin, normal, adminSession, normalSession);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration["Authentication:Jwt:Issuer"] = "m3-admin-safety";
            builder.Configuration["Authentication:Jwt:Audience"] = "m3-admin-safety-tests";
            builder.Configuration["Authentication:Jwt:SigningSecret"] = "m3-test-only-signing-key-for-admin-safety";
            builder.Services.AddControllers().AddApplicationPart(typeof(AdminSafetyController).Assembly);
            builder.Services.AddWayhunterSessionAuthentication(builder.Configuration);
            builder.Services.AddAdminSafetyMonitoring();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IAuthenticatedSessionReader>(data);
            builder.Services.AddSingleton<ITrustedAdministratorRoleReader>(data);
            builder.Services.AddScoped<TrustedAdministratorRoleResolutionService>();
            builder.Services.AddSingleton<ITripMembershipRevocationRepository>(members);
            builder.Services.AddSingleton<ILiveTripSessionRepository>(sessions);
            builder.Services.AddSingleton<ITripLiveLocationAccessReader>(consent);
            if (registerReadAdapters)
            {
                builder.Services.AddSingleton<IAdminSafetyScopeReader>(data);
                builder.Services.AddSingleton<IAdminSafetyProjectionReader>(data);
                builder.Services.AddSingleton<IAdminSafetyAuditSink>(data);
            }

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            return new(app, admin, normal, adminSession, normalSession, data, participant, other.SessionId, consent, trip, session, members);
        }

        private static UserAccount CreateAccount(string name, DateTimeOffset now)
        {
            var account = UserAccount.RegisterPending(name, $"{name}@example.test", "test-hash", now.AddHours(-2));
            account.ActivateAfterVerifiedOtp(now.AddHours(-1));
            return account;
        }

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }

    private sealed class DataSource(AdminSafetyTarget target, AdminSafetyProjection projection, UserAccount admin,
        UserAccount normal, IdentitySession adminSession, IdentitySession normalSession)
        : IAuthenticatedSessionReader, ITrustedAdministratorRoleReader, IAdminSafetyScopeReader,
          IAdminSafetyProjectionReader, IAdminSafetyAuditSink
    {
        public AdminSafetyTarget Target { get; } = target;
        public bool RoleUnavailable { get; set; }
        public AdministratorRoleAssignmentStatus RoleStatus { get; set; } = AdministratorRoleAssignmentStatus.Active;
        public AdminSafetySections Sections { get; set; } = AdminSafetySections.Participants | AdminSafetySections.Hazards |
            AdminSafetySections.Separation | AdminSafetySections.SafeCheckIn;
        public AdminSafetySections LastReadSections { get; private set; }
        public bool WrongProjection { get; set; }
        public bool GrantRemoved { get; set; }
        public bool AuditUnavailable { get; set; }
        public Func<Task>? OnProjection { get; set; }
        public Func<Task>? OnAudit { get; set; }
        public Func<AdminSafetyProjection, AdminSafetyProjection>? TransformProjection { get; set; }
        public List<AdminSafetyAuditRecord> Audit { get; } = [];

        public Task<UserAccount?> FindAccountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Find(new[] { admin, normal }, account => account.UserId == userId));

        public Task<IdentitySession?> FindSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Find(new[] { adminSession, normalSession }, session => session.SessionId == sessionId));

        public Task<AdministratorRoleAssignment?> FindAssignmentAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (RoleUnavailable)
                throw new InvalidOperationException("Role source unavailable.");
            return Task.FromResult<AdministratorRoleAssignment?>(userId == admin.UserId ? new(userId, "Admin", RoleStatus) : null);
        }

        public Task<AdminSafetyGrant?> ReadGrantAsync(string actor, AdminSafetyTarget requested, CancellationToken ct) =>
            Task.FromResult<AdminSafetyGrant?>(!GrantRemoved && actor == admin.UserId && requested.TripPlanId == Target.TripPlanId
                ? new(requested, actor, Sections) : null);

        public async Task<AdminSafetyProjection?> ReadAsync(AdminSafetyGrant grant, CancellationToken ct)
        {
            LastReadSections = grant.Sections;
            if (OnProjection is { } callback)
            {
                OnProjection = null;
                await callback();
            }
            var fresh = projection with { ObservedAtUtc = DateTimeOffset.UtcNow };
            if (WrongProjection)
                return fresh with { Target = new(Guid.NewGuid(), Guid.NewGuid()) };
            return TransformProjection?.Invoke(fresh) ?? fresh;
        }

        public async Task RecordAsync(AdminSafetyAuditRecord record, CancellationToken ct)
        {
            if (AuditUnavailable)
                throw new InvalidOperationException("Audit unavailable.");
            Audit.Add(record);
            if (OnAudit is { } callback)
            {
                OnAudit = null;
                await callback();
            }
        }
    }
}
