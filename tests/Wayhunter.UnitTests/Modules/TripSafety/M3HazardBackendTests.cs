
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Controllers.Mobile.TripSafety;

using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3HazardBackendTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = CreatedAt;

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    private sealed class FakeRoutesProvider : IRoutesProvider
    {
        public bool Available { get; set; } = true;

        public Task<RouteProviderResult> CalculateRouteAsync(
            IReadOnlyList<RouteWaypoint> orderedStops,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Available)
            {
                return Task.FromResult(new RouteProviderResult(
                    false,
                    Array.Empty<RouteWaypoint>(),
                    null,
                    null,
                    null,
                    "Route provider unavailable."));
            }

            return Task.FromResult(new RouteProviderResult(
                true,
                new[]
                {
                    new RouteWaypoint(9.0, 80.0),
                    new RouteWaypoint(9.0, 80.1)
                },
                12,
                20,
                "TestRouteProvider",
                null));
        }
    }

    private sealed class Fixture
    {
        public string OwnerId { get; } =
            Guid.NewGuid().ToString("D");

        public string MemberId { get; } =
            Guid.NewGuid().ToString("D");

        public string OutsiderId { get; } =
            Guid.NewGuid().ToString("D");

        public string AdminId { get; } =
            Guid.NewGuid().ToString("D");

        public TripPlan Trip { get; }

        public MutableClock Clock { get; } = new();

        public FakeRoutesProvider Routes { get; } = new();

        public InMemoryTripPlanRepository Trips { get; } =
            new();

        public InMemoryTripMembershipRevocationRepository Members
        { get; } = new();

        public InMemoryHazardReportRepository Hazards { get; } =
            new();

        public Fixture()
        {
            Trip = new TripPlan(
                OwnerId,
                "M3 Hazard SQL Readiness Trip",
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 22),
                12000m,
                "Van",
                4);

            Trip.AddStop(
                "Start",
                latitude: 9.0,
                longitude: 80.0);

            Trip.AddStop(
                "Destination",
                latitude: 9.0,
                longitude: 80.1);
        }

        public async Task SeedAsync()
        {
            await Trips.AddAsync(Trip);
            await Members.AddTripAsync(Trip);
        }

        public async Task AddMemberAsync()
        {
            var invitation = TripInvitation.Create(
                Guid.NewGuid(),
                Trip.Id,
                OwnerId,
                MemberId,
                CreatedAt.AddMinutes(-10));

            invitation.Accept(CreatedAt.AddMinutes(-5));

            var member = TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation);

            await Members.AddMemberAsync(member);
        }

        public TripHazardService CreateService()
        {
            return new TripHazardService(
                Hazards,
                Trips,
                Members,
                Routes,
                Clock);
        }

        public CreateTripHazardRequest Request(
            double latitude = 9.005,
            double longitude = 80.05)
        {
            return new CreateTripHazardRequest(
                latitude,
                longitude,
                "Fallen tree blocking road",
                "private/hazards/test-photo-001",
                CreatedAt.AddMinutes(-5));
        }

        public TripHazardsController Controller(
            string? userId,
            bool admin = false)
        {
            var controller = new TripHazardsController(
                CreateService());

            var claims = new List<Claim>();

            if (userId is not null)
            {
                claims.Add(new Claim(
                    ClaimTypes.NameIdentifier,
                    userId));

                claims.Add(new Claim(
                    "sub",
                    userId));

                if (admin)
                {
                    claims.Add(new Claim(
                        ClaimTypes.Role,
                        "Admin"));
                }
            }

            var identity = new ClaimsIdentity(
                claims,
                userId is null ? null : "TestAuth");

            controller.ControllerContext =
                new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(identity)
                    }
                };

            return controller;
        }
    }

    private static async Task<Fixture> SetupAsync()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        return fixture;
    }

    // TEST 01
    [Fact]
    public async Task Report_ShouldStartUnverified()
    {
        var f = await SetupAsync();

        var result = await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        Assert.Equal(
            HazardStatus.Reported,
            result.Status);

        Assert.False(result.IsVerified);
        Assert.True(result.HasPhotoEvidence);

        Assert.Equal(
            CreatedAt,
            result.ReportedAtUtc);

        Assert.Equal(
            CreatedAt.AddHours(24),
            result.ExpiresAtUtc);

        Assert.Equal(
            CreatedAt.AddMinutes(-5),
            result.PhotoCapturedAtUtc);
    }

    // TEST 02
    [Theory]
    [InlineData(double.NaN, 80.0)]
    [InlineData(91.0, 80.0)]
    [InlineData(9.0, 181.0)]
    public async Task Report_ShouldRejectInvalidCoordinates(
        double latitude,
        double longitude)
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.CreateService().ReportAsync(
                f.Trip.Id,
                f.OwnerId,
                f.Request(latitude, longitude)));
    }

    // TEST 03
    [Fact]
    public async Task FuturePhotoCaptureTime_ShouldBeRejected()
    {
        var f = await SetupAsync();

        var request = f.Request() with
        {
            PhotoCapturedAtUtc = CreatedAt.AddMinutes(10)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.CreateService().ReportAsync(
                f.Trip.Id,
                f.OwnerId,
                request));
    }

    // TEST 04
    [Fact]
    public async Task Outsider_CannotReportOrReadHazards()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.CreateService().ReportAsync(
                f.Trip.Id,
                f.OutsiderId,
                f.Request()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OutsiderId));
    }

    // TEST 05
    [Fact]
    public async Task ActiveMember_CanReadRelevantWarnings()
    {
        var f = await SetupAsync();

        await f.AddMemberAsync();

        await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        var warnings =
            await f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.MemberId);

        Assert.Single(warnings);
    }

    // TEST 06
    [Fact]
    public async Task NearbyHazard_ShouldMatchActualRouteGeometry()
    {
        var f = await SetupAsync();

        await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        var warnings =
            await f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OwnerId);

        var warning = Assert.Single(warnings);

        Assert.Equal(
            HazardStatus.Reported,
            warning.Status);
    }

    // TEST 07
    [Fact]
    public async Task DistantHazard_ShouldNotBeBroadcast()
    {
        var f = await SetupAsync();

        await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request(latitude: 9.03));

        var warnings =
            await f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OwnerId);

        Assert.Empty(warnings);
    }

    // TEST 08
    [Fact]
    public async Task MissingRouteGeometry_ShouldFailClosed()
    {
        var f = await SetupAsync();

        f.Routes.Available = false;

        await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        var warnings =
            await f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OwnerId);

        Assert.Empty(warnings);
    }

    // TEST 09
    [Fact]
    public async Task Verify_ShouldRecordAdminAndPreserveWarning()
    {
        var f = await SetupAsync();

        var reported = await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        f.Clock.Now = CreatedAt.AddMinutes(1);

        var verified = await f.CreateService().VerifyAsync(
            reported.HazardId,
            f.AdminId);

        Assert.Equal(
            HazardStatus.Verified,
            verified.Status);

        Assert.True(verified.IsVerified);
        Assert.Equal(f.Clock.Now, verified.VerifiedAtUtc);

        var warnings =
            await f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OwnerId);

        Assert.Single(warnings);
    }

    // TEST 10
    [Fact]
    public async Task Reporter_CannotVerifyOwnHazard()
    {
        var f = await SetupAsync();

        var reported = await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.CreateService().VerifyAsync(
                reported.HazardId,
                f.OwnerId));
    }

    // TEST 11
    [Fact]
    public async Task Resolve_ShouldRemoveWarningFromActiveFeed()
    {
        var f = await SetupAsync();

        var reported = await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        f.Clock.Now = CreatedAt.AddMinutes(2);

        var resolved = await f.CreateService().ResolveAsync(
            reported.HazardId,
            f.AdminId,
            "Road cleared");

        Assert.Equal(
            HazardStatus.Resolved,
            resolved.Status);

        var warnings =
            await f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OwnerId);

        Assert.Empty(warnings);
    }

    // TEST 12
    [Fact]
    public async Task Expiry_ShouldBeIdempotentAndPreserveHistory()
    {
        var f = await SetupAsync();

        var reported = await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        f.Clock.Now = CreatedAt.AddHours(24);

        // Warning query is time-aware even before worker.
        var warnings =
            await f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OwnerId);

        Assert.Empty(warnings);

        var first = await f.CreateService().ExpireDueAsync();
        var second = await f.CreateService().ExpireDueAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        var stored = await f.Hazards.GetByIdAsync(
            reported.HazardId);

        Assert.NotNull(stored);

        Assert.Equal(
            HazardStatus.Expired,
            stored.Status);

        Assert.Equal(
            CreatedAt.AddHours(24),
            stored.ExpiredAtUtc);
    }

    // TEST 13
    [Fact]
    public async Task CancelledTrip_ShouldNotReceiveHazards()
    {
        var f = await SetupAsync();

        f.Trip.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.CreateService().GetWarningsForTripAsync(
                f.Trip.Id,
                f.OwnerId));
    }

    // TEST 14
    [Fact]
    public async Task Api_RequiresTrustedIdentity()
    {
        var f = await SetupAsync();

        var controller = f.Controller(null);

        var result = await controller.Report(
            f.Trip.Id,
            f.Request(),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    // TEST 15
    [Fact]
    public async Task NonAdmin_CannotVerifyThroughApi()
    {
        var f = await SetupAsync();

        var reported = await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        var controller = f.Controller(f.OutsiderId);

        var result = await controller.Verify(
            reported.HazardId,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(
                result).StatusCode);
    }

    // TEST 16
    [Fact]
    public async Task Admin_CanVerifyThroughApi()
    {
        var f = await SetupAsync();

        var reported = await f.CreateService().ReportAsync(
            f.Trip.Id,
            f.OwnerId,
            f.Request());

        f.Clock.Now = CreatedAt.AddMinutes(1);

        var controller = f.Controller(
            f.AdminId,
            admin: true);

        var result = await controller.Verify(
            reported.HazardId,
            CancellationToken.None);

        var response = Assert.IsType<HazardWarningDto>(
            Assert.IsType<OkObjectResult>(
                result).Value);

        Assert.Equal(
            HazardStatus.Verified,
            response.Status);
    }
}
