
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.HistoricalRoutes;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GetHistoricalRouteSegmentsServiceTests
{
    private sealed class FakeEvidenceReader
        : ITrustedHistoricalEvidenceReader
    {
        public M3VerifiedHistoricalEvidence? Evidence { get; set; }

        public int Calls { get; private set; }

        public Task<M3VerifiedHistoricalEvidence?> GetForViewerAsync(
            Guid tripPlanId,
            string authenticatedViewerUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(Evidence);
        }
    }

    private static (
        TripPlan Trip,
        M3VerifiedHistoricalEvidence Evidence
    ) CreateFixture()
    {
        var trip = new TripPlan(
            "owner-01",
            "Jaffna Tour",
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 16),
            10000m,
            "Car",
            2);

        trip.AddStop("Jaffna");
        trip.AddStop("Nallur");
        trip.AddStop("Point Pedro");

        var sessionId = Guid.NewGuid();

        var startedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

        var visits = trip.Stops
            .Select((stop, index) => new M3VerifiedStopVisit(
                trip.Id,
                sessionId,
                stop.Id,
                index,
                startedAt.AddMinutes(index * 10)))
            .ToArray();

        var evidence = new M3VerifiedHistoricalEvidence(
            trip.Id,
            sessionId,
            "viewer-01",
            true,
            true,
            LiveTripStatus.Finished,
            startedAt,
            startedAt.AddHours(1),
            visits);

        return (trip, evidence);
    }

    [Fact]
    public async Task ValidEvidence_ReturnsTwoSegments()
    {
        var (trip, evidence) = CreateFixture();

        var repo = new InMemoryTripPlanRepository();
        await repo.AddAsync(trip);

        var reader = new FakeEvidenceReader
        {
            Evidence = evidence
        };

        var service = new GetHistoricalRouteSegmentsService(
            repo, reader);

        var result = await service.GetAsync(
            trip.Id, "viewer-01");

        Assert.Equal(2, result.Count);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task UnknownTrip_ReturnsEmptyWithoutReadingM3()
    {
        var repo = new InMemoryTripPlanRepository();
        var reader = new FakeEvidenceReader();

        var service = new GetHistoricalRouteSegmentsService(
            repo, reader);

        var result = await service.GetAsync(
            Guid.NewGuid(), "viewer-01");

        Assert.Empty(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task MissingM3Evidence_ReturnsEmpty()
    {
        var (trip, _) = CreateFixture();

        var repo = new InMemoryTripPlanRepository();
        await repo.AddAsync(trip);

        var service = new GetHistoricalRouteSegmentsService(
            repo, new FakeEvidenceReader());

        var result = await service.GetAsync(
            trip.Id, "viewer-01");

        Assert.Empty(result);
    }

    [Fact]
    public async Task WrongViewer_ReturnsEmpty()
    {
        var (trip, evidence) = CreateFixture();

        var repo = new InMemoryTripPlanRepository();
        await repo.AddAsync(trip);

        var reader = new FakeEvidenceReader
        {
            Evidence = evidence
        };

        var service = new GetHistoricalRouteSegmentsService(
            repo, reader);

        var result = await service.GetAsync(
            trip.Id, "stranger-01");

        Assert.Empty(result);
    }

    [Fact]
    public async Task UnfinishedTrip_ReturnsEmpty()
    {
        var (trip, evidence) = CreateFixture();

        var repo = new InMemoryTripPlanRepository();
        await repo.AddAsync(trip);

        var reader = new FakeEvidenceReader
        {
            Evidence = evidence with
            {
                SessionStatus = LiveTripStatus.Active
            }
        };

        var service = new GetHistoricalRouteSegmentsService(
            repo, reader);

        var result = await service.GetAsync(
            trip.Id, "viewer-01");

        Assert.Empty(result);
    }
}
