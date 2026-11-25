using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class CopySuggestedTripPlanServiceTests
{
    [Fact]
    public void DomainCopy_MakesIndependentDraft_WithNewIdsAndSourceAttribution()
    {
        var source = MakeSource();
        var sourceStops = source.Stops.Select(x => x.Id).ToArray();
        var clone = TripPlan.CreateCopyFrom(source, "viewer-2");

        Assert.NotEqual(source.Id, clone.Id);
        Assert.Equal("viewer-2", clone.OwnerUserId);
        Assert.Equal(source.Id, clone.SourceTripPlanId);
        Assert.Equal("original-owner", clone.SourceOwnerUserId);
        Assert.Equal(source.TripName, clone.TripName);
        Assert.Equal(source.BudgetLkr, clone.BudgetLkr);
        Assert.Equal(source.Stops.Select(x => x.Name), clone.Stops.Select(x => x.Name));
        Assert.Equal(source.Stops.Select(x => x.PlaceId), clone.Stops.Select(x => x.PlaceId));
        Assert.Equal(new[] { 0, 1 }, clone.Stops.Select(x => x.OrderIndex));
        Assert.DoesNotContain(clone.Stops, x => sourceStops.Contains(x.Id));
        Assert.Equal(2, source.Stops.Count);
        Assert.Equal(sourceStops, source.Stops.Select(x => x.Id));
    }

    [Fact]
    public void EditingCopy_NeverMutatesSource()
    {
        var source = MakeSource();
        var clone = TripPlan.CreateCopyFrom(source, "viewer-2");
        var originalDates = (source.StartDate, source.EndDate);
        var originalStops = source.Stops.Select(stop =>
            (stop.Id, stop.OrderIndex, stop.Name, stop.PlaceId, stop.Latitude, stop.Longitude)).ToArray();

        clone.UpdateDetails("Changed trip", clone.StartDate.AddDays(2), clone.EndDate.AddDays(2),
            500m, clone.VehicleType, 1);
        clone.RemoveStop(clone.Stops.First().Id);
        var added = clone.AddStop("New draft stop");
        Assert.True(clone.ReorderStops(clone.Stops.Select(stop => stop.Id).Reverse().ToArray()));

        Assert.Equal(added.Id, clone.Stops.OrderBy(stop => stop.OrderIndex).First().Id);
        Assert.Equal("Original trip", source.TripName);
        Assert.Equal(1000m, source.BudgetLkr);
        Assert.Equal(originalDates, (source.StartDate, source.EndDate));
        Assert.Equal(originalStops, source.Stops.Select(stop =>
            (stop.Id, stop.OrderIndex, stop.Name, stop.PlaceId, stop.Latitude, stop.Longitude)));
        Assert.Equal(source.Id, clone.SourceTripPlanId);
        Assert.Equal(source.OwnerUserId, clone.SourceOwnerUserId);
    }

    [Fact]
    public void CopyRejectsMissingStopsAndSameOwner()
    {
        var source = MakeSource();
        Assert.Throws<InvalidOperationException>(
            () => TripPlan.CreateCopyFrom(source, "original-owner"));
        var empty = new TripPlan("original-owner", "Empty", new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 2), 0, "Car", 1);
        Assert.Throws<InvalidOperationException>(
            () => TripPlan.CreateCopyFrom(empty, "viewer-2"));
    }

    [Fact]
    public async Task DemoMode_AlwaysFailsClosed()
    {
        var service = new CopySuggestedTripPlanService(
            new UnavailableCopySuggestedTripPlanStore());
        Assert.Null(await service.CopyAsync(Guid.NewGuid(), "viewer-2", "test-key"));
    }

    [Fact]
    public async Task InvalidSourceOrIdentityRejectedBeforeStoreCall()
    {
        var store = new FakeStore();
        var service = new CopySuggestedTripPlanService(store);
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CopyAsync(Guid.Empty, "viewer-2", "test-key"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CopyAsync(Guid.NewGuid(), " ", "test-key"));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task UnauthorizedOrUnfinishedSource_ReturnsNoCopy()
    {
        var store = new FakeStore();
        var service = new CopySuggestedTripPlanService(store);
        Assert.Null(await service.CopyAsync(Guid.NewGuid(), "viewer-2", "test-key"));
    }

    [Fact]
    public async Task AuthorizedCopy_ReturnsNewDraftAndPrivateCreatorStaysServerSide()
    {
        var source = MakeSource();
        var store = new FakeStore
        {
            Result = TripPlan.CreateCopyFrom(source, "viewer-2")
        };
        var service = new CopySuggestedTripPlanService(store);
        var result = await service.CopyAsync(source.Id, "viewer-2", "test-key");

        Assert.NotNull(result);
        Assert.Equal(source.Id, result.SourceTripPlanId);
        Assert.Equal("viewer-2", store.ViewerId);
        Assert.Equal(2, result.Stops.Count);
        Assert.DoesNotContain("OwnerUserId", result.GetType().GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("SourceOwnerUserId", result.GetType().GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task StoreReturningWrongOwner_IsRejected()
    {
        var source = MakeSource();
        var store = new FakeStore { Result = TripPlan.CreateCopyFrom(source, "viewer-2") };
        var service = new CopySuggestedTripPlanService(store);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CopyAsync(source.Id, "not-the-viewer", "test-key"));
    }

    [Fact]
    public async Task StoreReturningCopyOfDifferentSource_IsRejected()
    {
        var requestedSource = MakeSource();
        var store = new FakeStore { Result = TripPlan.CreateCopyFrom(MakeSource(), "viewer-2") };
        var service = new CopySuggestedTripPlanService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CopyAsync(requestedSource.Id, "viewer-2", "test-key"));
    }

    private static TripPlan MakeSource()
    {
        var source = new TripPlan("original-owner", "Original trip",
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2),
            1000m, "Car", 2);
        source.AddStop("Jaffna", "place-1", 9.6615, 80.0255);
        source.AddStop("Point Pedro", "place-2", 9.8167, 80.2333);
        return source;
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("two,values")]
    [InlineData("café")]
    public async Task InvalidIdempotencyKey_DoesNotCallStore(string key)
    {
        var store = new FakeStore();
        var service = new CopySuggestedTripPlanService(store);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CopyAsync(Guid.NewGuid(), "viewer-2", key));
        Assert.Equal(0, store.CallCount);
    }

    private sealed class FakeStore : ICopySuggestedTripPlanStore
    {
        public Task<bool> SetReuseConsentAsync(Guid sourceTripPlanId, string ownerUserId, bool allowed, CancellationToken cancellationToken) => Task.FromResult(false);

        public int CallCount { get; private set; }
        public string? ViewerId { get; private set; }
        public TripPlan? Result { get; set; }

        public Task<TripPlan?> CopyEligibleAsync(Guid sourceTripPlanId,
            string authenticatedUserId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ViewerId = authenticatedUserId;
            return Task.FromResult(Result);
        }
    }
}
