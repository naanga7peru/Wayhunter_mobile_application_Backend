
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripSharingConsentTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset SessionAt =
        CreatedAt.AddMinutes(10);

    private static readonly DateTimeOffset ChangedAt =
        CreatedAt.AddHours(1);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return ChangedAt;
        }
    }

    private static TripMember CreateMember(
        Guid tripId,
        string userId = "friend-01")
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

    private static (
        Guid TripId,
        LiveTripSession Session,
        InMemoryTripSharingConsentRepository Repository,
        TripSharingConsentService Service
    ) Setup(bool seedMember = true)
    {
        var tripId = Guid.NewGuid();

        var session = LiveTripSession.Start(
            Guid.NewGuid(),
            tripId,
            SessionAt);

        var repository =
            new InMemoryTripSharingConsentRepository();

        repository.SeedSessionForTests(session);

        if (seedMember)
        {
            repository.SeedAcceptedMemberForTests(
                CreateMember(tripId));
        }

        var service = new TripSharingConsentService(
            repository,
            new FixedTimeProvider());

        return (
            tripId,
            session,
            repository,
            service);
    }

    private static TripSharingConsentController
        CreateController(
            TripSharingConsentService service,
            string? userId,
            bool authenticated)
    {
        var controller =
            new TripSharingConsentController(service);

        var context = new DefaultHttpContext();

        context.User = authenticated
            ? new ClaimsPrincipal(
                new ClaimsIdentity(
                    userId is null
                        ? Array.Empty<Claim>()
                        : new[]
                        {
                            new Claim(
                                ClaimTypes.NameIdentifier,
                                userId)
                        },
                    "test"))
            : new ClaimsPrincipal(
                new ClaimsIdentity());

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = context
            };

        return controller;
    }

    [Fact]
    public void Accepted_Membership_Does_Not_Create_Gps_Consent()
    {
        var f = Setup();

        Assert.Null(
            f.Repository.GetSnapshotForTests(
                f.Session.SessionId,
                "friend-01"));
    }

    [Fact]
    public async Task Accepted_Member_Can_Grant_Only_Own_Consent()
    {
        var f = Setup();

        var result = await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            grant: true);

        Assert.Equal("friend-01", result.UserId);
        Assert.Equal(f.Session.SessionId, result.SessionId);
        Assert.True(result.IsGranted);
        Assert.Equal(ChangedAt, result.GrantedAtUtc);
        Assert.Null(result.RevokedAtUtc);
    }

    [Fact]
    public async Task Member_Can_Immediately_Revoke_Gps()
    {
        var f = Setup();

        await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            true);

        var revoked =
            await f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-01",
                false);

        Assert.False(revoked.IsGranted);
        Assert.Equal(ChangedAt, revoked.RevokedAtUtc);

        Assert.False(
            f.Repository.GetSnapshotForTests(
                f.Session.SessionId,
                "friend-01")!.IsGranted);
    }

    [Fact]
    public async Task Repeated_Grant_Is_Idempotent()
    {
        var f = Setup();

        var first =
            await f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-01",
                true);

        var second =
            await f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-01",
                true);

        Assert.Equal(
            first.GrantedAtUtc,
            second.GrantedAtUtc);

        Assert.True(second.IsGranted);
    }

    [Fact]
    public async Task Repeated_Revoke_Remains_Off()
    {
        var f = Setup();

        await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            true);

        var first = await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            false);

        var second = await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            false);

        Assert.False(second.IsGranted);

        Assert.Equal(
            first.RevokedAtUtc,
            second.RevokedAtUtc);
    }

    [Fact]
    public async Task Revoke_Without_Previous_Grant_Keeps_Consent_Off()
    {
        var f = Setup();

        var result = await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            false);

        Assert.False(result.IsGranted);
        Assert.Null(result.GrantedAtUtc);
    }

    [Fact]
    public async Task NonMember_Cannot_Grant_Consent()
    {
        var f = Setup();

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "stranger-01",
                true));

        Assert.Null(
            f.Repository.GetSnapshotForTests(
                f.Session.SessionId,
                "stranger-01"));
    }

    [Fact]
    public async Task Member_Of_Another_Trip_Cannot_Change_Consent()
    {
        var f = Setup(seedMember: false);

        f.Repository.SeedAcceptedMemberForTests(
            CreateMember(Guid.NewGuid()));

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-01",
                true));
    }

    [Fact]
    public async Task Session_From_Different_Trip_Is_Denied()
    {
        var f = Setup();

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Service.SetMyConsentAsync(
                Guid.NewGuid(),
                f.Session.SessionId,
                "friend-01",
                true));
    }

    [Fact]
    public async Task Unknown_Session_Is_Not_Found()
    {
        var f = Setup();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                Guid.NewGuid(),
                "friend-01",
                true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unauthenticated_Request_Is_Denied(
        string? userId)
    {
        var f = Setup();

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                userId,
                true));
    }

    [Fact]
    public async Task Member_Id_Matching_Is_Case_Sensitive()
    {
        var f = Setup();

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "FRIEND-01",
                true));
    }

    [Fact]
    public async Task Whitespace_Is_Trimmed_On_Trusted_User_Id()
    {
        var f = Setup();

        var result = await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "  friend-01  ",
            true);

        Assert.Equal("friend-01", result.UserId);
    }

    [Fact]
    public async Task Finished_Session_Cannot_Change_Consent()
    {
        var f = Setup();

        f.Session.Finish(ChangedAt);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-01",
                true));
    }

    [Fact]
    public async Task Paused_Session_May_Store_Consent_But_Not_Publish()
    {
        var f = Setup();

        f.Session.Pause();

        var result = await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            true);

        Assert.True(result.IsGranted);

        Assert.Equal(
            Wayhunter.Domain.Modules.TripSafety.Enums
                .LiveTripStatus.Paused,
            f.Session.Status);
    }

    [Fact]
    public async Task Membership_Revocation_Disables_Consent_And_Blocks_Regrant()
    {
        var f = Setup();

        await f.Service.SetMyConsentAsync(
            f.TripId,
            f.Session.SessionId,
            "friend-01",
            true);

        f.Repository.RevokeMembershipForTests(
            f.TripId,
            "friend-01",
            ChangedAt.AddMinutes(2));

        Assert.False(
            f.Repository.GetSnapshotForTests(
                f.Session.SessionId,
                "friend-01")!.IsGranted);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-01",
                true));
    }

    [Fact]
    public async Task Concurrent_Grant_Requests_Are_Idempotent()
    {
        var f = Setup();

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() =>
                f.Service.SetMyConsentAsync(
                    f.TripId,
                    f.Session.SessionId,
                    "friend-01",
                    true)));

        var results = await Task.WhenAll(tasks);

        Assert.All(
            results,
            x => Assert.True(x.IsGranted));

        Assert.Single(
            results
                .Select(x => x.GrantedAtUtc)
                .Distinct());
    }

    [Fact]
    public async Task Invalid_Ids_Are_Rejected()
    {
        var f = Setup();

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.Service.SetMyConsentAsync(
                Guid.Empty,
                f.Session.SessionId,
                "friend-01",
                true));

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                Guid.Empty,
                "friend-01",
                true));
    }

    [Fact]
    public async Task Cancelled_Request_Does_Not_Change_Consent()
    {
        var f = Setup();

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => f.Service.SetMyConsentAsync(
                f.TripId,
                f.Session.SessionId,
                "friend-01",
                true,
                cts.Token));

        Assert.Null(
            f.Repository.GetSnapshotForTests(
                f.Session.SessionId,
                "friend-01"));
    }

    [Fact]
    public async Task Controller_Rejects_Unauthenticated_Request()
    {
        var f = Setup();

        var controller = CreateController(
            f.Service,
            null,
            authenticated: false);

        var action = await controller.SetMine(
            f.TripId,
            f.Session.SessionId,
            new SetMyTripGpsConsentRequest(true),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            action.Result);
    }

    [Fact]
    public async Task Controller_Rejects_Missing_Trusted_User_Claim()
    {
        var f = Setup();

        var controller = CreateController(
            f.Service,
            null,
            authenticated: true);

        var action = await controller.SetMine(
            f.TripId,
            f.Session.SessionId,
            new SetMyTripGpsConsentRequest(true),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            action.Result);
    }

    [Fact]
    public async Task Controller_Allows_Authenticated_Member()
    {
        var f = Setup();

        var controller = CreateController(
            f.Service,
            "friend-01",
            authenticated: true);

        var action = await controller.SetMine(
            f.TripId,
            f.Session.SessionId,
            new SetMyTripGpsConsentRequest(true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            action.Result);

        var dto = Assert.IsType<GpsConsentDto>(
            ok.Value);

        Assert.True(dto.IsGranted);
        Assert.Equal("friend-01", dto.UserId);
    }

    [Fact]
    public async Task Controller_Blocks_Authenticated_NonMember()
    {
        var f = Setup();

        var controller = CreateController(
            f.Service,
            "stranger-01",
            authenticated: true);

        var action = await controller.SetMine(
            f.TripId,
            f.Session.SessionId,
            new SetMyTripGpsConsentRequest(true),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(
            action.Result);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            result.StatusCode);
    }
}
