
using System;

using Wayhunter.Api.Hubs.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3SubscriptionCleanupTests
{
    [Fact]
    public void FinishedSession_RemovesOnlyMatchingSubscriptions()
    {
        var registry = new TripLocationSubscriberRegistry();

        var tripId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();

        registry.Register(
            "connection-a",
            tripId,
            sessionId,
            "user-a");

        registry.Register(
            "connection-b",
            tripId,
            sessionId,
            "user-b");

        registry.Register(
            "connection-c",
            tripId,
            otherSessionId,
            "user-c");

        var removed = registry.RevokeSession(
            tripId,
            sessionId);

        Assert.Equal(2, removed.Count);

        Assert.Null(registry.Find("connection-a"));
        Assert.Null(registry.Find("connection-b"));

        Assert.NotNull(registry.Find("connection-c"));
    }

    [Fact]
    public void RemovedMember_LosesAllSubscriptionsForTrip()
    {
        var registry = new TripLocationSubscriberRegistry();

        var tripId = Guid.NewGuid();
        var anotherTripId = Guid.NewGuid();

        registry.Register(
            "connection-a",
            tripId,
            Guid.NewGuid(),
            "user-a");

        registry.Register(
            "connection-b",
            tripId,
            Guid.NewGuid(),
            "user-b");

        registry.Register(
            "connection-c",
            anotherTripId,
            Guid.NewGuid(),
            "user-a");

        var removed = registry.RevokeTripMember(
            tripId,
            "user-a");

        Assert.Single(removed);

        Assert.Null(registry.Find("connection-a"));
        Assert.NotNull(registry.Find("connection-b"));
        Assert.NotNull(registry.Find("connection-c"));
    }

    [Fact]
    public void ConsentRevoke_IsLimitedToSelectedSession()
    {
        var registry = new TripLocationSubscriberRegistry();

        var tripId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var anotherSessionId = Guid.NewGuid();

        registry.Register(
            "connection-a",
            tripId,
            sessionId,
            "user-a");

        registry.Register(
            "connection-b",
            tripId,
            sessionId,
            "user-b");

        registry.Register(
            "connection-c",
            tripId,
            anotherSessionId,
            "user-a");

        var removed = registry.RevokeMemberFromSession(
            tripId,
            sessionId,
            "user-a");

        Assert.Single(removed);

        Assert.Null(registry.Find("connection-a"));

        Assert.NotNull(registry.Find("connection-b"));
        Assert.NotNull(registry.Find("connection-c"));
    }

    [Fact]
    public void RepeatedCleanup_IsIdempotent()
    {
        var registry = new TripLocationSubscriberRegistry();

        var tripId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        registry.Register(
            "connection-a",
            tripId,
            sessionId,
            "user-a");

        Assert.Single(
            registry.RevokeSession(
                tripId,
                sessionId));

        Assert.Empty(
            registry.RevokeSession(
                tripId,
                sessionId));
    }

    [Fact]
    public void InvalidRevocationInputs_AreRejected()
    {
        var registry = new TripLocationSubscriberRegistry();

        Assert.Throws<ArgumentException>(() =>
            registry.RevokeSession(
                Guid.Empty,
                Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() =>
            registry.RevokeTripMember(
                Guid.NewGuid(),
                ""));
    }
}
