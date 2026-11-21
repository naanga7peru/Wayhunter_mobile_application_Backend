using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Integrations.Maps;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3HazardSeparationIntegrationTests
{
    [Fact]
    public async Task DefaultHostResolvesInertServicesWithoutAuthoritativeRouteReader()
    {
        using var host = new WebApplicationFactory<Program>();
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.Null(services.GetService<IAuthoritativeTripHazardRouteReader>());
        var alerts = services.GetRequiredService<LiveTripSafetyAlerts>();
        Assert.Null(await alerts.ObserveHazardAsync(Guid.NewGuid(), Guid.NewGuid(), "owner", Guid.NewGuid()));
        var sample = new SafetyLocation(Guid.NewGuid(), Guid.NewGuid(), "owner", 0, 0, DateTimeOffset.UtcNow);
        Assert.Equal(SeparationChange.None, await alerts.EvaluateSeparationAsync(
            sample.TripPlanId, sample.SessionId, sample.UserId, sample, sample));
    }

    [Fact]
    public async Task HazardLifecycleUsesAuthoritativeGeometryAndSuppressesConcurrentRepeatedWarnings()
    {
        var fixture = await Fixture.CreateAsync();
        var report = await fixture.Hazards.ReportAsync(fixture.Trip.Id, "owner",
            new(9, 80.05, "Blocked road", "test/photo", fixture.Clock.Now));
        Task<HazardAlertUpdate?> Observe() => fixture.Alerts.ObserveHazardAsync(
            fixture.Trip.Id, fixture.Session.SessionId, "traveler", report.HazardId);
        Task<HazardAlertPhase> Transition(HazardAlertPhase target) => fixture.Alerts.TransitionHazardAsync(
            fixture.Trip.Id, fixture.Session.SessionId, "traveler", report.HazardId, target);

        Assert.Null(await Observe());
        fixture.Route.Geometry = [new(11, 80), new(11, 80.1)];
        Assert.Null(await Observe());
        fixture.Route.Geometry = [new(9, 80), new(9, 80.1)];
        var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(Observe)));
        var warning = Assert.Single(attempts, item => item?.ShouldWarn == true)!;
        Assert.Equal(HazardAlertPhase.Active, warning.Phase);
        Assert.Equal(report.PhotoCapturedAtUtc, warning.Warning.PhotoCapturedAtUtc);
        Assert.False(warning.Warning.IsVerified);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Transition(HazardAlertPhase.Resolved));

        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(29);
        Assert.False((await Observe())!.ShouldWarn);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        Assert.True((await Observe())!.ShouldWarn);
        Assert.Equal(HazardAlertPhase.Acknowledged, await Transition(HazardAlertPhase.Acknowledged));
        Assert.Equal(HazardAlertPhase.Acknowledged, await Transition(HazardAlertPhase.Acknowledged));
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(30);
        Assert.False((await Observe())!.ShouldWarn);
        Assert.Equal(HazardAlertPhase.Resolved, await Transition(HazardAlertPhase.Resolved));
        Assert.Equal(HazardAlertPhase.Resolved, await Transition(HazardAlertPhase.Resolved));
        Assert.False((await Observe())!.ShouldWarn);

        fixture.Clock.Now = report.ExpiresAtUtc;
        Assert.Null(await Observe());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Transition(HazardAlertPhase.Resolved));
    }

    [Fact]
    public async Task ExistingConsentReaderRejectsWrongScopeRevokedConsentAndClosedTrips()
    {
        var fixture = await Fixture.CreateAsync();
        var trip = fixture.Trip.Id;
        var session = fixture.Session.SessionId;
        var now = fixture.Clock.Now;
        var own = new SafetyLocation(trip, session, "traveler", 9, 80, now);
        var peer = new SafetyLocation(trip, session, "peer", 9, 81, now);
        Task<SeparationChange> Evaluate() => fixture.Alerts.EvaluateSeparationAsync(trip, session, own.UserId, own, peer);
        Assert.Equal(SeparationChange.Separated, await Evaluate());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Alerts.EvaluateSeparationAsync(trip, session, "outsider", own with { UserId = "outsider" }, peer));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Alerts.EvaluateSeparationAsync(trip, session, own.UserId, own, peer with { TripPlanId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Alerts.EvaluateSeparationAsync(trip, session, own.UserId, own, peer with { SessionId = Guid.NewGuid() }));
        await fixture.Access.SetMyConsentAsync(trip, session, "peer", false, now);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(Evaluate);
        await fixture.Access.SetMyConsentAsync(trip, session, "peer", true, now.AddSeconds(1));
        fixture.Session.Finish(now.AddSeconds(1));
        await Assert.ThrowsAsync<InvalidOperationException>(Evaluate);

        var cancelled = await Fixture.CreateAsync();
        cancelled.Trip.Cancel();
        await cancelled.Trips.UpdateAsync(cancelled.Trip);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cancelled.Alerts.ObserveHazardAsync(
            cancelled.Trip.Id, cancelled.Session.SessionId, "traveler", Guid.NewGuid()));
    }

    private sealed class ClockStub : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RouteStub : IAuthoritativeTripHazardRouteReader
    {
        public Guid ExpectedTrip { get; init; }
        public IReadOnlyList<RouteWaypoint>? Geometry { get; set; }
        public Task<IReadOnlyList<RouteWaypoint>?> ReadAsync(Guid tripPlanId, CancellationToken cancellationToken) =>
            Task.FromResult(tripPlanId == ExpectedTrip ? Geometry : null);
    }

    private sealed class Fixture
    {
        public ClockStub Clock { get; } = new();
        public InMemoryTripSharingConsentRepository Access { get; } = new();
        public InMemoryTripPlanRepository Trips { get; } = new();
        public TripPlan Trip { get; } = new("owner", "Test", new(2026, 11, 21), new(2026, 11, 22), 0, "Car", 3);
        public LiveTripSession Session { get; }
        public TripHazardService Hazards { get; }
        public RouteStub Route { get; }
        public LiveTripSafetyAlerts Alerts { get; }
        private readonly InMemoryTripMembershipRevocationRepository _members = new();

        private Fixture()
        {
            Session = LiveTripSession.Start(Guid.NewGuid(), Trip.Id, Clock.Now);
            Hazards = new(new InMemoryHazardReportRepository(), Trips, _members, new UnavailableHazardRoutesProvider(), Clock);
            Route = new() { ExpectedTrip = Trip.Id };
            Alerts = new(Hazards, Trips, Access, new(), new(TimeSpan.FromSeconds(30), 100, 50, TimeSpan.FromSeconds(15)), Clock, Route);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var trip = fixture.Trip.Id;
            var now = fixture.Clock.Now;
            await fixture.Trips.AddAsync(fixture.Trip);
            await fixture._members.AddTripAsync(fixture.Trip);
            fixture.Access.SeedSessionForTests(fixture.Session);
            foreach (var user in new[] { "traveler", "peer" })
            {
                var invitation = TripInvitation.Create(Guid.NewGuid(), trip, "owner", user, now.AddMinutes(-1));
                invitation.Accept(now);
                var member = TripMember.FromAcceptedInvitation(Guid.NewGuid(), invitation);
                await fixture._members.AddMemberAsync(member);
                fixture.Access.SeedAcceptedMemberForTests(member);
                await fixture.Access.SetMyConsentAsync(trip, fixture.Session.SessionId, user, true, now);
            }

            return fixture;
        }
    }
}