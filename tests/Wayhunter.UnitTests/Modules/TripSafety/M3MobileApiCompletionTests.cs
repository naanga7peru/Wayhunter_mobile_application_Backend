
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Api.Services.TripSafety;

using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3MobileApiCompletionTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset Now =
        CreatedAt.AddHours(2);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    private sealed class Fixture
    {
        public string OwnerId { get; } =
            Guid.NewGuid().ToString("D");

        public string RecipientId { get; } =
            Guid.NewGuid().ToString("D");

        public string OutsiderId { get; } =
            Guid.NewGuid().ToString("D");

        public TripPlan Trip { get; }

        public TripInvitation Invitation { get; }

        public InMemoryLiveTripSessionRepository Sessions { get; } =
            new();

        public InMemoryTripInvitationDecisionRepository Invitations
        { get; } = new();

        public InMemoryTripMembershipRevocationRepository Members
        { get; } = new();

        public Fixture()
        {
            Trip = new TripPlan(
                OwnerId,
                "M3 API Verification Trip",
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 22),
                15000m,
                "Van",
                5);

            Invitation = TripInvitation.Create(
                Guid.NewGuid(),
                Trip.Id,
                OwnerId,
                RecipientId,
                CreatedAt);
        }

        public async Task SeedAsync()
        {
            await Members.AddTripAsync(Trip);
            await Invitations.AddAsync(Invitation);
        }

        public async Task AddActiveMemberAsync()
        {
            var accepted = TripInvitation.Create(
                Guid.NewGuid(),
                Trip.Id,
                OwnerId,
                RecipientId,
                CreatedAt);

            accepted.Accept(JoinedAt);

            var member = TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                accepted);

            await Members.AddMemberAsync(member);
        }

        public TripSafetyMobileActionsController Controller(
            string? userId,
            string? subjectOverride = null,
            bool duplicateNameId = false,
            bool authenticated = true)
        {
            var clock = new FixedClock();

            var access = new TripSafetyTripAccessService(
                Members);

            var controller =
                new TripSafetyMobileActionsController(
                    new LiveTripSessionService(
                        Sessions,
                        clock),

                    new LiveTripSessionRecoveryService(
                        Sessions,
                        access),

                    new TripInvitationDecisionService(
                        Invitations,
                        clock,
                        new TripInviteBlockReaderStub()),

                    new TripMembershipRevocationService(
                        Members,
                        clock),

                    Sessions,
                    access);

            var claims = new List<Claim>();

            if (userId is not null)
            {
                claims.Add(new Claim(
                    ClaimTypes.NameIdentifier,
                    userId));

                claims.Add(new Claim(
                    "sub",
                    subjectOverride ?? userId));

                if (duplicateNameId)
                {
                    claims.Add(new Claim(
                        ClaimTypes.NameIdentifier,
                        userId));
                }
            }

            var identity = new ClaimsIdentity(
                claims,
                authenticated ? "TestAuthentication" : null);

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        return fixture;
    }

    private static async Task<Guid> StartAsOwnerAsync(
        Fixture fixture)
    {
        var controller = fixture.Controller(
            fixture.OwnerId);

        var result = await controller.Start(
            fixture.Trip.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);

        var session =
            Assert.IsType<TripSessionDto>(ok.Value);

        return session.SessionId;
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        var attributes =
            typeof(TripSafetyMobileActionsController)
                .GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    true);

        Assert.NotEmpty(attributes);
    }

    [Fact]
    public async Task Start_OnlyOwner_AndRepeatedStartIsIdempotent()
    {
        var fixture = await CreateFixtureAsync();

        var outsider = fixture.Controller(
            fixture.OutsiderId);

        var forbidden = await outsider.Start(
            fixture.Trip.Id,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(
                forbidden).StatusCode);

        var firstId = await StartAsOwnerAsync(fixture);

        var owner = fixture.Controller(
            fixture.OwnerId);

        var repeated = await owner.Start(
            fixture.Trip.Id,
            CancellationToken.None);

        var repeatedSession =
            Assert.IsType<TripSessionDto>(
                Assert.IsType<OkObjectResult>(
                    repeated).Value);

        Assert.Equal(
            firstId,
            repeatedSession.SessionId);
    }

    [Fact]
    public async Task MissingOrConflictingIdentity_ReturnsUnauthorized()
    {
        var fixture = await CreateFixtureAsync();

        var anonymous = fixture.Controller(
            null,
            authenticated: false);

        var anonymousResult = await anonymous.Start(
            fixture.Trip.Id,
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            anonymousResult);

        var conflicting = fixture.Controller(
            fixture.OwnerId,
            subjectOverride: fixture.RecipientId);

        var conflictingResult = await conflicting.Start(
            fixture.Trip.Id,
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            conflictingResult);

        var duplicated = fixture.Controller(
            fixture.OwnerId,
            duplicateNameId: true);

        var duplicatedResult = await duplicated.Start(
            fixture.Trip.Id,
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            duplicatedResult);
    }

    [Fact]
    public async Task Owner_CanPauseResumeAndFinish()
    {
        var fixture = await CreateFixtureAsync();

        var sessionId = await StartAsOwnerAsync(fixture);

        var controller = fixture.Controller(
            fixture.OwnerId);

        var pauseResult = await controller.Pause(
            fixture.Trip.Id,
            sessionId,
            CancellationToken.None);

        var paused = Assert.IsType<TripSessionDto>(
            Assert.IsType<OkObjectResult>(
                pauseResult).Value);

        Assert.Equal(
            LiveTripStatus.Paused,
            paused.Status);

        var resumeResult = await controller.Resume(
            fixture.Trip.Id,
            sessionId,
            CancellationToken.None);

        var resumed = Assert.IsType<TripSessionDto>(
            Assert.IsType<OkObjectResult>(
                resumeResult).Value);

        Assert.Equal(
            LiveTripStatus.Active,
            resumed.Status);

        var finishResult = await controller.Finish(
            fixture.Trip.Id,
            sessionId,
            CancellationToken.None);

        var finished = Assert.IsType<TripSessionDto>(
            Assert.IsType<OkObjectResult>(
                finishResult).Value);

        Assert.Equal(
            LiveTripStatus.Finished,
            finished.Status);

        var invalidPause = await controller.Pause(
            fixture.Trip.Id,
            sessionId,
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(
            invalidPause);
    }

    [Fact]
    public async Task Recovery_RequiresActiveMembership()
    {
        var fixture = await CreateFixtureAsync();

        var sessionId = await StartAsOwnerAsync(fixture);

        await fixture.AddActiveMemberAsync();

        var member = fixture.Controller(
            fixture.RecipientId);

        var allowed = await member.Recover(
            fixture.Trip.Id,
            sessionId,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(allowed);

        var outsider = fixture.Controller(
            fixture.OutsiderId);

        var denied = await outsider.Recover(
            fixture.Trip.Id,
            sessionId,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(
                denied).StatusCode);

        var owner = fixture.Controller(
            fixture.OwnerId);

        var removed = await owner.RemoveMember(
            fixture.Trip.Id,
            fixture.RecipientId,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(removed);

        var afterRemoval = await member.Recover(
            fixture.Trip.Id,
            sessionId,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(
                afterRemoval).StatusCode);
    }

    [Fact]
    public async Task WrongTripSessionCombination_ReturnsNotFound()
    {
        var fixture = await CreateFixtureAsync();

        var sessionId = await StartAsOwnerAsync(fixture);

        var secondTrip = new TripPlan(
            fixture.OwnerId,
            "Another Owned Trip",
            new DateOnly(2026, 11, 21),
            new DateOnly(2026, 11, 23),
            20000m,
            "Car",
            3);

        await fixture.Members.AddTripAsync(secondTrip);

        var owner = fixture.Controller(
            fixture.OwnerId);

        var result = await owner.Pause(
            secondTrip.Id,
            sessionId,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);

        var original = await fixture.Sessions.GetByIdAsync(
            sessionId);

        Assert.NotNull(original);

        Assert.Equal(
            LiveTripStatus.Active,
            original.Status);
    }

    [Fact]
    public async Task Recipient_CanAcceptOnlyOnce()
    {
        var fixture = await CreateFixtureAsync();

        var owner = fixture.Controller(
            fixture.OwnerId);

        var forbidden = await owner.AcceptInvitation(
            fixture.Invitation.Id,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(
                forbidden).StatusCode);

        var recipient = fixture.Controller(
            fixture.RecipientId);

        var first = await recipient.AcceptInvitation(
            fixture.Invitation.Id,
            CancellationToken.None);

        var firstResponse =
            Assert.IsType<TripInvitationDecisionApiResponse>(
                Assert.IsType<OkObjectResult>(
                    first).Value);

        Assert.Equal(
            TripInvitationStatus.Accepted,
            firstResponse.Status);

        Assert.NotNull(firstResponse.MemberId);

        var second = await recipient.AcceptInvitation(
            fixture.Invitation.Id,
            CancellationToken.None);

        var repeated =
            Assert.IsType<TripInvitationDecisionApiResponse>(
                Assert.IsType<OkObjectResult>(
                    second).Value);

        Assert.Equal(
            firstResponse.MemberId,
            repeated.MemberId);

        var storedMember =
            await fixture.Invitations.GetMemberByInvitationIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(storedMember);
        Assert.Equal(firstResponse.MemberId, storedMember.Id);
    }

    [Fact]
    public async Task Reject_OnlyRecipient_Cancel_OnlySender()
    {
        var rejectFixture = await CreateFixtureAsync();

        var recipient = rejectFixture.Controller(
            rejectFixture.RecipientId);

        var rejected = await recipient.RejectInvitation(
            rejectFixture.Invitation.Id,
            CancellationToken.None);

        var rejectResponse =
            Assert.IsType<TripInvitationDecisionApiResponse>(
                Assert.IsType<OkObjectResult>(
                    rejected).Value);

        Assert.Equal(
            TripInvitationStatus.Rejected,
            rejectResponse.Status);

        var cancelFixture = await CreateFixtureAsync();

        var wrongUser = cancelFixture.Controller(
            cancelFixture.RecipientId);

        var forbidden = await wrongUser.CancelInvitation(
            cancelFixture.Invitation.Id,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(
                forbidden).StatusCode);

        var sender = cancelFixture.Controller(
            cancelFixture.OwnerId);

        var cancelled = await sender.CancelInvitation(
            cancelFixture.Invitation.Id,
            CancellationToken.None);

        var cancelResponse =
            Assert.IsType<TripInvitationDecisionApiResponse>(
                Assert.IsType<OkObjectResult>(
                    cancelled).Value);

        Assert.Equal(
            TripInvitationStatus.Cancelled,
            cancelResponse.Status);
    }

    [Fact]
    public async Task MemberRemoval_OnlyOwner_AndIdempotent()
    {
        var fixture = await CreateFixtureAsync();

        await fixture.AddActiveMemberAsync();

        var recipient = fixture.Controller(
            fixture.RecipientId);

        var forbidden = await recipient.RemoveMember(
            fixture.Trip.Id,
            fixture.RecipientId,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(
                forbidden).StatusCode);

        var owner = fixture.Controller(
            fixture.OwnerId);

        var first = await owner.RemoveMember(
            fixture.Trip.Id,
            fixture.RecipientId,
            CancellationToken.None);

        var firstResponse =
            Assert.IsType<TripMemberRemovalApiResponse>(
                Assert.IsType<OkObjectResult>(
                    first).Value);

        Assert.True(firstResponse.Removed);

        var second = await owner.RemoveMember(
            fixture.Trip.Id,
            fixture.RecipientId,
            CancellationToken.None);

        var repeatedResponse =
            Assert.IsType<TripMemberRemovalApiResponse>(
                Assert.IsType<OkObjectResult>(
                    second).Value);

        Assert.False(repeatedResponse.Removed);

        var notices =
            await fixture.Members.GetQueuedNoticesAsync();

        Assert.Single(notices);
    }

    [Fact]
    public async Task Owner_CannotRemoveThemselves()
    {
        var fixture = await CreateFixtureAsync();

        var owner = fixture.Controller(
            fixture.OwnerId);

        var result = await owner.RemoveMember(
            fixture.Trip.Id,
            fixture.OwnerId,
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task UnknownInvitation_ReturnsNotFound()
    {
        var fixture = await CreateFixtureAsync();

        var recipient = fixture.Controller(
            fixture.RecipientId);

        var result = await recipient.AcceptInvitation(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
