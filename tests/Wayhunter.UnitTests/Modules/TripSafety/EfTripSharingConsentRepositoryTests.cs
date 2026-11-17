using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class EfTripSharingConsentRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

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

    private static WayhunterSharedDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(
                    $"m3-consent-{Guid.NewGuid()}")
                .ConfigureWarnings(
                    warnings => warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static TripMember CreateMember(
        Guid tripPlanId,
        string userId)
    {
        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            tripPlanId,
            "owner-01",
            userId,
            CreatedAt);

        invitation.Accept(JoinedAt);

        return TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);
    }

    private static async Task<(
        WayhunterSharedDbContext DbContext,
        Guid TripPlanId,
        LiveTripSession Session,
        EfTripSharingConsentRepository Repository,
        TripSharingConsentService Service)>
        CreateFixtureAsync(
            bool includeMember = true)
    {
        var dbContext = CreateDbContext();

        var tripPlanId = Guid.NewGuid();

        var session = LiveTripSession.Start(
            Guid.NewGuid(),
            tripPlanId,
            SessionAt);

        dbContext.LiveTripSessions.Add(session);

        if (includeMember)
        {
            dbContext.TripMembers.Add(
                CreateMember(
                    tripPlanId,
                    "friend-01"));
        }

        await dbContext.SaveChangesAsync();

        var repository =
            new EfTripSharingConsentRepository(
                dbContext);

        var service =
            new TripSharingConsentService(
                repository,
                new FixedTimeProvider());

        return (
            dbContext,
            tripPlanId,
            session,
            repository,
            service);
    }

    [Fact]
    public async Task Accepted_Member_Can_Persist_Gps_Consent()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var result =
            await fixture.Service.SetMyConsentAsync(
                fixture.TripPlanId,
                fixture.Session.SessionId,
                "friend-01",
                grant: true);

        Assert.True(result.IsGranted);

        Assert.Equal(
            "friend-01",
            result.UserId);

        var stored =
            await dbContext.TripGpsConsents
                .SingleAsync();

        Assert.True(stored.IsGranted);

        Assert.Equal(
            fixture.Session.SessionId,
            stored.SessionId);

        Assert.Equal(
            "friend-01",
            stored.UserId);

        Assert.Equal(
            ChangedAt,
            stored.GrantedAtUtc);

        Assert.Null(
            stored.RevokedAtUtc);
    }

    [Fact]
    public async Task Persisted_Consent_Can_Be_Revoked()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        await fixture.Service.SetMyConsentAsync(
            fixture.TripPlanId,
            fixture.Session.SessionId,
            "friend-01",
            grant: true);

        var result =
            await fixture.Service.SetMyConsentAsync(
                fixture.TripPlanId,
                fixture.Session.SessionId,
                "friend-01",
                grant: false);

        Assert.False(
            result.IsGranted);

        var stored =
            await dbContext.TripGpsConsents
                .SingleAsync();

        Assert.False(
            stored.IsGranted);

        Assert.Equal(
            ChangedAt,
            stored.RevokedAtUtc);
    }

    [Fact]
    public async Task Repeated_Grant_Does_Not_Create_Duplicate_Row()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        await fixture.Service.SetMyConsentAsync(
            fixture.TripPlanId,
            fixture.Session.SessionId,
            "friend-01",
            grant: true);

        await fixture.Service.SetMyConsentAsync(
            fixture.TripPlanId,
            fixture.Session.SessionId,
            "friend-01",
            grant: true);

        Assert.Equal(
            1,
            await dbContext.TripGpsConsents.CountAsync());

        var stored =
            await dbContext.TripGpsConsents
                .SingleAsync();

        Assert.True(
            stored.IsGranted);

        Assert.Equal(
            ChangedAt,
            stored.GrantedAtUtc);
    }

    [Fact]
    public async Task NonMember_Cannot_Persist_Gps_Consent()
    {
        var fixture =
            await CreateFixtureAsync(
                includeMember: false);

        await using var dbContext = fixture.DbContext;

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () =>
                fixture.Service.SetMyConsentAsync(
                    fixture.TripPlanId,
                    fixture.Session.SessionId,
                    "stranger-01",
                    grant: true));

        Assert.Equal(
            0,
            await dbContext.TripGpsConsents.CountAsync());
    }

    [Fact]
    public async Task Finished_Session_Cannot_Change_Consent()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        fixture.Session.Finish(
            ChangedAt.AddMinutes(-1));

        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                fixture.Service.SetMyConsentAsync(
                    fixture.TripPlanId,
                    fixture.Session.SessionId,
                    "friend-01",
                    grant: true));

        Assert.Equal(
            0,
            await dbContext.TripGpsConsents.CountAsync());
    }

    [Fact]
    public async Task Different_Trip_Id_Is_Denied()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () =>
                fixture.Service.SetMyConsentAsync(
                    Guid.NewGuid(),
                    fixture.Session.SessionId,
                    "friend-01",
                    grant: true));

        Assert.Equal(
            0,
            await dbContext.TripGpsConsents.CountAsync());
    }

    [Fact]
    public async Task Explicit_Consent_Is_Required_For_Live_Location()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        await Assert.ThrowsAnyAsync<Exception>(
            () =>
                fixture.Repository.EnsureAllowedAsync(
                    fixture.TripPlanId,
                    fixture.Session.SessionId,
                    "friend-01"));

        await fixture.Service.SetMyConsentAsync(
            fixture.TripPlanId,
            fixture.Session.SessionId,
            "friend-01",
            grant: true);

        await fixture.Repository.EnsureAllowedAsync(
            fixture.TripPlanId,
            fixture.Session.SessionId,
            "friend-01");
    }
}
