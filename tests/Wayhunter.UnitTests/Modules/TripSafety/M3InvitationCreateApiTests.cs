
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Api.Services.TripSafety;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3InvitationCreateApiTests
{
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(
                2026, 11, 19, 10, 0, 0,
                TimeSpan.Zero);
        }
    }

    private sealed class FakeEligibility
        : ITripInviteEligibilityAccess
    {
        public bool Active { get; set; } = true;

        public bool Blocked { get; set; }

        public bool Called { get; private set; }

        public Task<TripInviteRecipientEligibility> CheckAsync(
            string senderUserId,
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            Called = true;

            return Task.FromResult(
                new TripInviteRecipientEligibility(
                    Active,
                    Blocked));
        }
    }

    private sealed record Fixture(
        TripPlan Trip,
        string Owner,
        string Recipient,
        FakeEligibility Eligibility,
        TripInvitationCreateService Service,
        InMemoryTripInvitationDecisionRepository Decisions,
        InMemoryTripInvitationCreateStore Store);

    private static async Task<Fixture> SetupAsync()
    {
        var owner = Guid.NewGuid().ToString("D");

        var recipient = Guid.NewGuid().ToString("D");

        var trip = new TripPlan(
            owner,
            "Safe Trip",
            new DateOnly(2026, 12, 1),
            new DateOnly(2026, 12, 2),
            1000m,
            "Car",
            2);

        var members =
            new InMemoryTripMembershipRevocationRepository();

        await members.AddTripAsync(trip);

        var decisions =
            new InMemoryTripInvitationDecisionRepository();

        var store =
            new InMemoryTripInvitationCreateStore(
                decisions);

        var eligibility = new FakeEligibility();

        var service = new TripInvitationCreateService(
            members,
            store,
            eligibility,
            new FixedClock());

        return new Fixture(
            trip,
            owner,
            recipient,
            eligibility,
            service,
            decisions,
            store);
    }

    [Fact]
    public async Task Owner_Creates_Pending_Invitation()
    {
        var f = await SetupAsync();

        var invitation = await f.Service.CreateAsync(
            f.Trip.Id,
            f.Owner,
            f.Recipient);

        Assert.Equal(
            TripInvitationStatus.Pending,
            invitation.Status);

        Assert.Equal(
            f.Owner,
            invitation.SenderUserId);

        Assert.Equal(
            f.Recipient,
            invitation.RecipientUserId);

        var stored =
            await f.Decisions.GetByIdAsync(
                invitation.Id);

        Assert.NotNull(stored);
    }

    [Fact]
    public async Task Recipient_Can_Read_Own_Pending_Invitation()
    {
        var f = await SetupAsync();

        await f.Service.CreateAsync(
            f.Trip.Id,
            f.Owner,
            f.Recipient);

        var own =
            await f.Service.GetMyPendingAsync(
                f.Recipient);

        var another =
            await f.Service.GetMyPendingAsync(
                Guid.NewGuid().ToString("D"));

        Assert.Single(own);
        Assert.Empty(another);
    }

    [Fact]
    public async Task Duplicate_Pending_Invitation_IsRejected()
    {
        var f = await SetupAsync();

        await f.Service.CreateAsync(
            f.Trip.Id,
            f.Owner,
            f.Recipient);

        await Assert.ThrowsAsync<
            InvalidOperationException>(() =>
                f.Service.CreateAsync(
                    f.Trip.Id,
                    f.Owner,
                    f.Recipient));
    }

    [Fact]
    public async Task Blocked_Relationship_IsDenied()
    {
        var f = await SetupAsync();

        f.Eligibility.Blocked = true;

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(() =>
                f.Service.CreateAsync(
                    f.Trip.Id,
                    f.Owner,
                    f.Recipient));

        var pending =
            await f.Store.GetPendingForRecipientAsync(
                f.Recipient);

        Assert.Empty(pending);
    }

    [Fact]
    public async Task Inactive_Recipient_IsDenied()
    {
        var f = await SetupAsync();

        f.Eligibility.Active = false;

        await Assert.ThrowsAsync<
            InvalidOperationException>(() =>
                f.Service.CreateAsync(
                    f.Trip.Id,
                    f.Owner,
                    f.Recipient));
    }

    [Fact]
    public async Task NonOwner_IsDenied_Before_M1_Lookup()
    {
        var f = await SetupAsync();

        var anotherUser =
            Guid.NewGuid().ToString("D");

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(() =>
                f.Service.CreateAsync(
                    f.Trip.Id,
                    anotherUser,
                    f.Recipient));

        Assert.False(f.Eligibility.Called);
    }

    [Fact]
    public async Task Owner_Cannot_Invite_Self()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<
            InvalidOperationException>(() =>
                f.Service.CreateAsync(
                    f.Trip.Id,
                    f.Owner,
                    f.Owner));
    }

    [Fact]
    public async Task Invalid_Recipient_Guid_IsDenied()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<
            ArgumentException>(() =>
                f.Service.CreateAsync(
                    f.Trip.Id,
                    f.Owner,
                    "not-a-guid"));
    }

    [Fact]
    public async Task Cancelled_Trip_Cannot_Create_Invitation()
    {
        var f = await SetupAsync();

        f.Trip.Cancel();

        await Assert.ThrowsAsync<
            InvalidOperationException>(() =>
                f.Service.CreateAsync(
                    f.Trip.Id,
                    f.Owner,
                    f.Recipient));
    }

    [Fact]
    public async Task Created_Invite_Can_Be_Accepted_Without_AutoGps()
    {
        var f = await SetupAsync();

        var invitation = await f.Service.CreateAsync(
            f.Trip.Id,
            f.Owner,
            f.Recipient);

        var decisions = new TripInvitationDecisionService(
            f.Decisions,
            new FixedClock());

        var member = await decisions.AcceptAsync(
            invitation.Id,
            f.Recipient);

        Assert.NotNull(member);

        Assert.Equal(
            invitation.Id,
            member.InvitationId);

        var pending =
            await f.Store.GetPendingForRecipientAsync(
                f.Recipient);

        Assert.Empty(pending);

        // The create/accept repositories only work
        // with TripInvitation and TripMember.
        // They do not grant TripGpsConsent.
    }

    [Fact]
    public async Task Concurrent_Creates_Never_Produce_Duplicates()
    {
        var f = await SetupAsync();

        var attempts = Enumerable
            .Range(0, 12)
            .Select(async _ =>
            {
                try
                {
                    await f.Service.CreateAsync(
                        f.Trip.Id,
                        f.Owner,
                        f.Recipient);

                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });

        var results = await Task.WhenAll(attempts);

        Assert.Single(
            results.Where(x => x));

        var pending =
            await f.Store.GetPendingForRecipientAsync(
                f.Recipient);

        Assert.Single(pending);
    }

    [Fact]
    public async Task Controller_Requires_Trusted_Identity()
    {
        var f = await SetupAsync();

        var controller =
            new TripInvitationCreateController(
                f.Service)
            {
                ControllerContext =
                    new ControllerContext
                    {
                        HttpContext =
                            new DefaultHttpContext()
                    }
            };

        var response = await controller.Create(
            f.Trip.Id,
            new CreateTripInvitationRequest(
                f.Recipient),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            response);
    }

    [Fact]
    public async Task Missing_M1_BlockReader_Fails_Closed()
    {
        var reader =
            new UnconfiguredTripInviteBlockRelationshipReader();

        await Assert.ThrowsAsync<
            TripInvitePrerequisiteUnavailableException>(() =>
                reader.IsBlockedEitherWayAsync(
                    Guid.NewGuid().ToString("D"),
                    Guid.NewGuid().ToString("D")));
    }

    [Fact]
    public async Task Missing_ServerSide_Eligibility_Fails_Closed()
    {
        var reader =
            new UnavailableTripInviteEligibilityAccess();

        await Assert.ThrowsAsync<
            TripInvitePrerequisiteUnavailableException>(() =>
                reader.CheckAsync(
                    Guid.NewGuid().ToString("D"),
                    Guid.NewGuid().ToString("D")));
    }
}
