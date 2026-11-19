using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GetSuggestedTripPlansServiceTests
{
    [Fact]
    public async Task DemoReader_ReturnsEmpty_AndDoesNotInventCompletedTrips()
    {
        var service = new GetSuggestedTripPlansService(
            new UnavailableSuggestedTripPlansReader());

        var result = await service.GetAsync("traveler-1");

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingAuthenticatedUser_IsRejected(string userId)
    {
        var service = new GetSuggestedTripPlansService(new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetAsync(userId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task InvalidLimit_IsRejected(int limit)
    {
        var service = new GetSuggestedTripPlansService(new FakeReader());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetAsync("traveler-1", limit));
    }

    [Fact]
    public async Task ReaderReceivesOnlyTrustedViewerAndBoundedLimit()
    {
        var reader = new FakeReader();
        var service = new GetSuggestedTripPlansService(reader);

        await service.GetAsync("traveler-1", 2);

        Assert.Equal("traveler-1", reader.LastUserId);
        Assert.Equal(2, reader.LastLimit);
    }

    [Fact]
    public async Task MalformedCompletionEvidence_IsNotPresented()
    {
        var reader = new FakeReader();
        reader.Results.Add(MakePlan(default));
        reader.Results.Add(MakePlan(DateTimeOffset.UtcNow));
        var service = new GetSuggestedTripPlansService(reader);

        var results = await service.GetAsync("traveler-1");

        Assert.Single(results);
    }

    private static SuggestedTripPlanDto MakePlan(DateTimeOffset completedAtUtc) =>
        new(Guid.NewGuid(), "Jaffna Tour",
            new DateOnly(2026, 11, 16),
            new DateOnly(2026, 11, 18),
            10000m, "Car", 2, 2, completedAtUtc);

    private sealed class FakeReader : ISuggestedTripPlansReader
    {
        public string? LastUserId { get; private set; }
        public int LastLimit { get; private set; }
        public List<SuggestedTripPlanDto> Results { get; } = new();

        public Task<IReadOnlyList<SuggestedTripPlanDto>> GetEligibleAsync(
            string authenticatedUserId,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            LastUserId = authenticatedUserId;
            LastLimit = maxResults;
            return Task.FromResult<IReadOnlyList<SuggestedTripPlanDto>>(Results);
        }
    }
}
