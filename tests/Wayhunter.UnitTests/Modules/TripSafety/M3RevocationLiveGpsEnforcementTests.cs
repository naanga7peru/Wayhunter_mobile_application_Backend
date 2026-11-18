
using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Wayhunter.Api.Hubs.TripSafety;

using Wayhunter.Domain.Modules.TripSafety.Entities;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3RevocationLiveGpsEnforcementTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(1);

    private static readonly DateTimeOffset SessionAt =
        CreatedAt.AddMinutes(2);

    private static readonly DateTimeOffset ConsentAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        CreatedAt.AddMinutes(10);

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

    [Fact]
    public async Task RemovedMember_CannotPublishLocation()
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
            CreateMember(tripId, "member-a"));

        await repository.SetMyConsentAsync(
            tripId,
            session.SessionId,
            "member-a",
            true,
            ConsentAt);

        // Access is allowed before removal.
        await repository.EnsureAllowedAsync(
            tripId,
            session.SessionId,
            "member-a");

        repository.RevokeMembershipForTests(
            tripId,
            "member-a",
            RemovedAt);

        // The same identity can no longer publish.
        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => repository.EnsureAllowedAsync(
                tripId,
                session.SessionId,
                "member-a"));
    }

    [Fact]
    public async Task RemovedMember_CannotReceiveFutureGpsUpdates()
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
            CreateMember(tripId, "member-a"));

        repository.SeedAcceptedMemberForTests(
            CreateMember(tripId, "member-b"));

        await repository.SetMyConsentAsync(
            tripId,
            session.SessionId,
            "member-a",
            true,
            ConsentAt);

        await repository.SetMyConsentAsync(
            tripId,
            session.SessionId,
            "member-b",
            true,
            ConsentAt);

        var registry =
            new TripLocationSubscriberRegistry();

        registry.Register(
            "connection-a",
            tripId,
            session.SessionId,
            "member-a");

        registry.Register(
            "connection-b",
            tripId,
            session.SessionId,
            "member-b");

        var fanout = new TripLocationFanoutService(
            repository,
            registry);

        var before =
            await fanout.GetAuthorizedRecipientsAsync(
                tripId,
                session.SessionId);

        Assert.Equal(2, before.Count);

        // Remove member-b while the subscription exists.
        repository.RevokeMembershipForTests(
            tripId,
            "member-b",
            RemovedAt);

        var after =
            await fanout.GetAuthorizedRecipientsAsync(
                tripId,
                session.SessionId);

        var allowed = Assert.Single(after);

        Assert.Equal("member-a", allowed.UserId);

        Assert.Null(
            registry.Find("connection-b"));

        Assert.NotNull(
            registry.Find("connection-a"));
    }

    [Fact]
    public async Task RemovedMember_CannotGrantGpsConsentAgain()
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
            CreateMember(tripId, "member-a"));

        await repository.SetMyConsentAsync(
            tripId,
            session.SessionId,
            "member-a",
            true,
            ConsentAt);

        repository.RevokeMembershipForTests(
            tripId,
            "member-a",
            RemovedAt);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => repository.SetMyConsentAsync(
                tripId,
                session.SessionId,
                "member-a",
                true,
                RemovedAt.AddMinutes(1)));
    }

    [Fact]
    public async Task SqlRepository_DeniesInactiveMemberEvenWithOldConsent()
    {
        // Provider-independent EF authorization test.
        // This is NOT a real SQL Server integration test.

        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(
                    $"m3-gps-revocation-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        await using var db =
            new WayhunterSharedDbContext(options);

        var tripId = Guid.NewGuid();

        var session = LiveTripSession.Start(
            Guid.NewGuid(),
            tripId,
            SessionAt);

        var member = CreateMember(
            tripId,
            "member-a");

        db.LiveTripSessions.Add(session);
        db.TripMembers.Add(member);

        await db.SaveChangesAsync();

        var repository =
            new EfTripSharingConsentRepository(db);

        await repository.SetMyConsentAsync(
            tripId,
            session.SessionId,
            "member-a",
            true,
            ConsentAt);

        await repository.EnsureAllowedAsync(
            tripId,
            session.SessionId,
            "member-a");

        // Simulate the successful membership state
        // transition persisted by the revocation flow.
        Assert.True(member.Remove(RemovedAt));

        await db.SaveChangesAsync();

        // Prevent authorization from using tracked state.
        db.ChangeTracker.Clear();

        // Historical consent still exists.
        var oldConsent =
            await db.TripGpsConsents
                .AsNoTracking()
                .SingleAsync(x =>
                    x.SessionId == session.SessionId &&
                    x.UserId == "member-a");

        Assert.True(oldConsent.IsGranted);

        // Active membership is required regardless
        // of the historical consent value.
        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => repository.EnsureAllowedAsync(
                tripId,
                session.SessionId,
                "member-a"));

        // Removed members cannot grant consent again.
        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => repository.SetMyConsentAsync(
                tripId,
                session.SessionId,
                "member-a",
                true,
                RemovedAt.AddMinutes(1)));
    }
}
