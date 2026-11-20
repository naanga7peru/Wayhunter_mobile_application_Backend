
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Api.Hubs.TripSafety;

using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3FinalTrustedIdentityTests
{
    private sealed class FakeHubContext : HubCallerContext
    {
        public FakeHubContext(ClaimsPrincipal user)
        {
            User = user;
        }

        public override string ConnectionId => "test-connection";

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal User { get; }

        public override IDictionary<object, object?> Items
        { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features
        { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted
            => CancellationToken.None;

        public override void Abort()
        {
        }
    }

    private static ClaimsPrincipal Principal(
        string? nameId,
        string? subject,
        bool authenticated = true,
        bool duplicateNameId = false)
    {
        var claims = new List<Claim>();

        if (nameId is not null)
        {
            claims.Add(new Claim(
                ClaimTypes.NameIdentifier,
                nameId));
        }

        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }

        if (duplicateNameId && nameId is not null)
        {
            claims.Add(new Claim(
                ClaimTypes.NameIdentifier,
                nameId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticated ? "TestAuth" : null));
    }

    private static TripSharingConsentController Controller(
        ClaimsPrincipal principal)
    {
        var repository =
            new InMemoryTripSharingConsentRepository();

        var service = new TripSharingConsentService(
            repository,
            TimeProvider.System);

        return new TripSharingConsentController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            }
        };
    }

    private static TripLocationHub Hub(
        ClaimsPrincipal principal)
    {
        var repository =
            new InMemoryTripSharingConsentRepository();

        var registry =
            new TripLocationSubscriberRegistry();

        var fanout = new TripLocationFanoutService(
            repository,
            registry);

        return new TripLocationHub(
            repository,
            registry,
            fanout,
            TimeProvider.System)
        {
            Context = new FakeHubContext(principal)
        };
    }

    [Fact]
    public async Task GpsConsent_RejectsConflictingClaims()
    {
        var principal = Principal(
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"));

        var response = await Controller(principal).SetMine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new SetMyTripGpsConsentRequest(true),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task GpsConsent_RejectsDuplicateClaims()
    {
        var id = Guid.NewGuid().ToString("D");

        var response = await Controller(
            Principal(id, id, duplicateNameId: true))
            .SetMine(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new SetMyTripGpsConsentRequest(true),
                CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task GpsConsent_RejectsMalformedIdentity()
    {
        var response = await Controller(
            Principal("not-a-guid", null))
            .SetMine(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new SetMyTripGpsConsentRequest(true),
                CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task GpsConsent_RejectsAnonymousPrincipal()
    {
        var id = Guid.NewGuid().ToString("D");

        var response = await Controller(
            Principal(id, id, authenticated: false))
            .SetMine(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new SetMyTripGpsConsentRequest(true),
                CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task SignalR_RejectsConflictingIdentity()
    {
        var hub = Hub(Principal(
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D")));

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinTripAsync(
                Guid.NewGuid(),
                Guid.NewGuid()));

        Assert.Equal(
            "Trusted user identity is required.",
            exception.Message);
    }

    [Fact]
    public async Task SignalR_RejectsDuplicateIdentity()
    {
        var id = Guid.NewGuid().ToString("D");

        var hub = Hub(Principal(
            id,
            id,
            duplicateNameId: true));

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinTripAsync(
                Guid.NewGuid(),
                Guid.NewGuid()));

        Assert.Equal(
            "Trusted user identity is required.",
            exception.Message);
    }

    [Fact]
    public async Task SignalR_RejectsMalformedIdentity()
    {
        var hub = Hub(Principal("not-a-guid", null));

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinTripAsync(
                Guid.NewGuid(),
                Guid.NewGuid()));

        Assert.Equal(
            "Trusted user identity is required.",
            exception.Message);
    }

    [Fact]
    public async Task SignalR_RejectsAnonymousPrincipal()
    {
        var id = Guid.NewGuid().ToString("D");

        var hub = Hub(Principal(
            id,
            id,
            authenticated: false));

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinTripAsync(
                Guid.NewGuid(),
                Guid.NewGuid()));

        Assert.Equal(
            "Trusted user identity is required.",
            exception.Message);
    }

    [Fact]
    public async Task SignalR_ValidIdentityStillRequiresTripAccess()
    {
        var id = Guid.NewGuid().ToString("D");

        var hub = Hub(Principal(id, id));

        // Authentication alone must not grant GPS access.
        // No session or active membership is seeded.
        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinTripAsync(
                Guid.NewGuid(),
                Guid.NewGuid()));

        Assert.NotEqual(
            "Trusted user identity is required.",
            exception.Message);
    }
}
