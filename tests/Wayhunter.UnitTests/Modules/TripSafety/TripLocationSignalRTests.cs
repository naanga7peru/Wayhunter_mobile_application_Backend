using System;
using System.Linq;
using System.Threading.Tasks;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripLocationSignalRTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(1);

    private static readonly DateTimeOffset SessionAt =
        CreatedAt.AddMinutes(2);

    private static readonly DateTimeOffset ChangedAt =
        CreatedAt.AddMinutes(10);

    private sealed record Fixture(
        Guid TripId,
        LiveTripSession Session,
        InMemoryTripSharingConsentRepository Repository,
        TripLocationSubscriberRegistry Registry,
        TripLocationFanoutService Fanout);

    private static TripMember CreateMember(
        Guid tripId,
        string userId)
    {
        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            tripId,
            "owner-01",
            userId,
            CreatedAt);

        invitation.Accept(JoinedAt);

        return TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);
    }

    private static Fixture Setup()
    {
        var tripId = Guid.NewGuid();

        var session = LiveTripSession.Start(
            Guid.NewGuid(),
            tripId,
            SessionAt);

        var repository =
            new InMemoryTripSharingConsentRepository();

        repository.SeedSessionForTests(session);

        repository.SeedAcceptedMemberForTests(
            CreateMember(tripId, "friend-a"));

        repository.SeedAcceptedMemberForTests(
            CreateMember(tripId, "friend-b"));

        var registry =
            new TripLocationSubscriberRegistry();

        var fanout = new TripLocationFanoutService(
            repository,
            registry);

        return new Fixture(
            tripId,
            session,
            repository,
            registry,
            fanout);
    }

    private static async Task GrantAsync(
        Fixture f,
        string userId)
    {
        await f.Repository.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            userId,
            true,
            ChangedAt);
    }

    [Fact]
    public async Task Invitation_Acceptance_Does_Not_Authorize_Gps()
    {
        var f = Setup();

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Repository.EnsureAllowedAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-a"));
    }

    [Fact]
    public async Task Explicit_Consent_Allows_Active_Member()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        await f.Repository.EnsureAllowedAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-a");
    }

    [Fact]
    public async Task Stranger_Cannot_Access_Live_Gps()
    {
        var f = Setup();

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Repository.EnsureAllowedAsync(
                f.TripId,
                f.Session.SessionId,
                "stranger"));
    }

    [Fact]
    public async Task Another_Trip_Cannot_Access_Live_Gps()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Repository.EnsureAllowedAsync(
                Guid.NewGuid(),
                f.Session.SessionId,
                "friend-a"));
    }

    [Fact]
    public async Task Paused_Trip_Cannot_Publish_Location()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        f.Session.Pause();

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => f.Repository.EnsureAllowedAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-a"));
    }

    [Fact]
    public async Task Finished_Trip_Cannot_Publish_Location()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        f.Session.Finish(ChangedAt.AddMinutes(1));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => f.Repository.EnsureAllowedAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-a"));
    }

    [Fact]
    public async Task Revoke_Consent_Blocks_Future_Location()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        await f.Repository.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-a",
            false,
            ChangedAt.AddMinutes(1));

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Repository.EnsureAllowedAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-a"));
    }

    [Fact]
    public async Task Subscriber_Without_Consent_Receives_Nothing()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        f.Registry.Register(
            "connection-a",
            f.TripId,
            f.Session.SessionId,
            "friend-a");

        f.Registry.Register(
            "connection-b",
            f.TripId,
            f.Session.SessionId,
            "friend-b");

        var recipients =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Single(recipients);

        Assert.Equal(
            "connection-a",
            recipients[0].ConnectionId);

        Assert.Null(
            f.Registry.Find("connection-b"));
    }

    [Fact]
    public async Task Two_Consenting_Members_Are_Eligible()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");
        await GrantAsync(f, "friend-b");

        f.Registry.Register(
            "connection-a",
            f.TripId,
            f.Session.SessionId,
            "friend-a");

        f.Registry.Register(
            "connection-b",
            f.TripId,
            f.Session.SessionId,
            "friend-b");

        var recipients =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Equal(2, recipients.Count);
    }

    [Fact]
    public async Task Revoked_Receiver_Is_Removed_From_Delivery()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");
        await GrantAsync(f, "friend-b");

        f.Registry.Register(
            "connection-a",
            f.TripId,
            f.Session.SessionId,
            "friend-a");

        f.Registry.Register(
            "connection-b",
            f.TripId,
            f.Session.SessionId,
            "friend-b");

        // Initial authorized recipients.
        var before =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Equal(2, before.Count);

        // Recipient revokes explicit GPS permission.
        await f.Repository.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-b",
            false,
            ChangedAt.AddMinutes(1));

        var after =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Single(after);

        Assert.Equal(
            "connection-a",
            after[0].ConnectionId);

        Assert.Null(
            f.Registry.Find("connection-b"));
    }

    [Fact]
    public async Task Removed_Member_Is_Excluded()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");
        await GrantAsync(f, "friend-b");

        f.Registry.Register(
            "connection-a",
            f.TripId,
            f.Session.SessionId,
            "friend-a");

        f.Registry.Register(
            "connection-b",
            f.TripId,
            f.Session.SessionId,
            "friend-b");

        f.Repository.RevokeMembershipForTests(
            f.TripId,
            "friend-b",
            ChangedAt.AddMinutes(2));

        var recipients =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Single(recipients);

        Assert.Equal(
            "friend-a",
            recipients[0].UserId);
    }

    [Fact]
    public async Task Other_Session_Subscriber_Is_Excluded()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        f.Registry.Register(
            "correct-connection",
            f.TripId,
            f.Session.SessionId,
            "friend-a");

        f.Registry.Register(
            "another-session",
            f.TripId,
            Guid.NewGuid(),
            "friend-a");

        var recipients =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Single(recipients);

        Assert.Equal(
            "correct-connection",
            recipients[0].ConnectionId);
    }

    [Fact]
    public async Task Other_Trip_Subscriber_Is_Excluded()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        f.Registry.Register(
            "correct-connection",
            f.TripId,
            f.Session.SessionId,
            "friend-a");

        f.Registry.Register(
            "other-trip",
            Guid.NewGuid(),
            f.Session.SessionId,
            "friend-a");

        var recipients =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Single(recipients);
    }

    [Fact]
    public async Task Paused_Trip_Has_No_Eligible_Receivers()
    {
        var f = Setup();

        await GrantAsync(f, "friend-a");

        f.Registry.Register(
            "connection-a",
            f.TripId,
            f.Session.SessionId,
            "friend-a");

        f.Session.Pause();

        var recipients =
            await f.Fanout.GetAuthorizedRecipientsAsync(
                f.TripId,
                f.Session.SessionId);

        Assert.Empty(recipients);
    }

    [Fact]
    public void Disconnect_Removes_Subscription()
    {
        var registry =
            new TripLocationSubscriberRegistry();

        registry.Register(
            "connection-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "friend-a");

        Assert.NotNull(
            registry.Find("connection-1"));

        registry.Remove("connection-1");

        Assert.Null(
            registry.Find("connection-1"));
    }

    [Fact]
    public void Reassigned_Connection_Is_Not_Removed_By_Stale_Cleanup()
    {
        var registry =
            new TripLocationSubscriberRegistry();

        var oldTripId = Guid.NewGuid();
        var newTripId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        registry.Register(
            "connection-1",
            oldTripId,
            sessionId,
            "friend-a");

        var old = registry.Find("connection-1");

        Assert.NotNull(old);

        registry.Register(
            "connection-1",
            newTripId,
            sessionId,
            "friend-a");

        registry.RemoveIfUnchanged(old!);

        Assert.Equal(
            newTripId,
            registry.Find("connection-1")!.TripPlanId);
    }

    [Fact]
    public void Valid_Coordinates_Create_Server_Timestamped_Event()
    {
        var tripId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var request = new PublishTripLocationRequest(
            9.6684,
            80.0074,
            8.5);

        var result =
            TripLocationInputValidator.CreateEvent(
                tripId,
                sessionId,
                "friend-a",
                request,
                ChangedAt);

        Assert.Equal(tripId, result.TripPlanId);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal("friend-a", result.UserId);
        Assert.Equal(9.6684, result.Latitude);
        Assert.Equal(80.0074, result.Longitude);
        Assert.Equal(ChangedAt, result.RecordedAtUtc);
    }

    [Theory]
    [InlineData(91, 80, 10)]
    [InlineData(-91, 80, 10)]
    [InlineData(9, 181, 10)]
    [InlineData(9, -181, 10)]
    [InlineData(9, 80, -1)]
    [InlineData(9, 80, 6000)]
    public void Invalid_Location_Is_Rejected(
        double latitude,
        double longitude,
        double accuracy)
    {
        var request = new PublishTripLocationRequest(
            latitude,
            longitude,
            accuracy);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => TripLocationInputValidator.CreateEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "friend-a",
                request,
                ChangedAt));
    }

    [Fact]
    public void Missing_Identity_Cannot_Create_Location_Event()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => TripLocationInputValidator.CreateEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "",
                new PublishTripLocationRequest(
                    9.6684,
                    80.0074,
                    10),
                ChangedAt));
    }
}
