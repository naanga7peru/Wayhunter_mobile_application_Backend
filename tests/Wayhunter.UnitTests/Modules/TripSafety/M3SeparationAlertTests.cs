using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3SeparationAlertTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 1, 0, 0, TimeSpan.Zero);
    private static readonly double Threshold = 2 * 6371000 *
        Math.Asin(Math.Sqrt(Math.Pow(Math.Sin(Math.PI / 6), 2)));

    [Theory]
    [InlineData(59.99, SeparationChange.None)]
    [InlineData(60, SeparationChange.Separated)]
    [InlineData(60.01, SeparationChange.Separated)]
    public async Task SeparationBoundary(double longitude, SeparationChange expected)
    {
        var (service, own, peer) = await CreateAsync();
        peer = peer with { Longitude = longitude };
        Assert.Equal(expected, await service.EvaluateSeparationAsync(own.TripPlanId, own.SessionId, own.UserId, own, peer));
    }

    [Fact]
    public async Task ConcurrentSamplesAndReversedPairEmitOneAlertThenRecover()
    {
        var (service, own, peer) = await CreateAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(() =>
            index % 2 == 0
                ? service.EvaluateSeparationAsync(own.TripPlanId, own.SessionId, own.UserId, own, peer)
                : service.EvaluateSeparationAsync(own.TripPlanId, own.SessionId, peer.UserId, peer, own))));
        Assert.Single(results, result => result == SeparationChange.Separated);

        var close = peer with { Longitude = 0, RecordedAtUtc = Now };
        Assert.Equal(SeparationChange.Recovered, await service.EvaluateSeparationAsync(own.TripPlanId, own.SessionId, own.UserId, own, close));
        Assert.Equal(SeparationChange.None, await service.EvaluateSeparationAsync(own.TripPlanId, own.SessionId, own.UserId, own, peer));
    }

    [Fact]
    public async Task StaleFutureAndInvalidSamplesDoNotMutateEpisode()
    {
        var (service, own, peer) = await CreateAsync();
        Task<SeparationChange> Evaluate(SafetyLocation sample) =>
            service.EvaluateSeparationAsync(own.TripPlanId, own.SessionId, own.UserId, own, sample);

        Assert.Equal(SeparationChange.InvalidLocation, await Evaluate(peer with { Latitude = double.NaN }));
        Assert.Equal(SeparationChange.InvalidLocation, await Evaluate(peer with { Longitude = 181 }));
        Assert.Equal(SeparationChange.StaleLocation, await Evaluate(peer with { RecordedAtUtc = Now.AddTicks(1) }));
        Assert.Equal(SeparationChange.StaleLocation, await Evaluate(peer with { RecordedAtUtc = Now.AddSeconds(-5).AddTicks(-1) }));
        Assert.Equal(SeparationChange.Separated, await Evaluate(peer with { RecordedAtUtc = Now.AddSeconds(-5) }));
        Assert.Equal(SeparationChange.StaleLocation, await Evaluate(peer with { Longitude = 0, RecordedAtUtc = Now.AddSeconds(-6) }));
        Assert.Equal(SeparationChange.None, await Evaluate(peer with { RecordedAtUtc = Now }));
    }

    private static async Task<(LiveTripSafetyAlerts Service, SafetyLocation Own, SafetyLocation Peer)> CreateAsync()
    {
        var trips = new InMemoryTripPlanRepository();
        var trip = new TripPlan("one", "test", new(2026, 11, 21), new(2026, 11, 22), 0, "Car", 2);
        await trips.AddAsync(trip);
        var session = Guid.NewGuid();
        var clock = new FixedClock();
        var hazards = new TripHazardService(new InMemoryHazardReportRepository(), trips,
            new InMemoryTripMembershipRevocationRepository(),
            new Wayhunter.Infrastructure.Integrations.Maps.UnavailableHazardRoutesProvider(), clock);
        var service = new LiveTripSafetyAlerts(hazards, trips, new AllowedAccess(), new(),
            new(SeparationMeters: Threshold, RecoveryMeters: 0, MaximumLocationAge: TimeSpan.FromSeconds(5)), clock);
        return (service, new(trip.Id, session, "one", 0, 0, Now.AddSeconds(-1)),
            new(trip.Id, session, "two", 0, 61, Now.AddSeconds(-1)));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class AllowedAccess : ITripLiveLocationAccessReader
    {
        public Task EnsureAllowedAsync(Guid tripPlanId, Guid sessionId, string authenticatedUserId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}