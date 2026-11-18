using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class EfTripInvitationDecisionRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DecisionAt =
        CreatedAt.AddMinutes(5);

    private static WayhunterSharedDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(
                    $"m3-invitation-{Guid.NewGuid()}")
                .ConfigureWarnings(
                    warnings => warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static TripInvitation CreateInvitation(
        Guid? invitationId = null,
        Guid? tripPlanId = null,
        string recipientUserId = "user-friend")
    {
        return TripInvitation.Create(
            invitationId ?? Guid.NewGuid(),
            tripPlanId ?? Guid.NewGuid(),
            "user-owner",
            recipientUserId,
            CreatedAt);
    }

    private static async Task<(
        WayhunterSharedDbContext DbContext,
        EfTripInvitationDecisionRepository Repository,
        TripInvitation Invitation)>
        CreateFixtureAsync()
    {
        var dbContext = CreateDbContext();

        var invitation = CreateInvitation();

        dbContext.TripInvitations.Add(invitation);

        await dbContext.SaveChangesAsync();

        var repository =
            new EfTripInvitationDecisionRepository(
                dbContext);

        return (
            dbContext,
            repository,
            invitation);
    }

    [Fact]
    public async Task GetById_Should_Return_Persisted_Invitation()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var stored =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            fixture.Invitation.Id,
            stored.Id);

        Assert.Equal(
            fixture.Invitation.TripPlanId,
            stored.TripPlanId);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);

        Assert.Null(
            stored.RespondedAtUtc);
    }

    [Fact]
    public async Task GetById_Should_Return_Null_When_Not_Found()
    {
        await using var dbContext = CreateDbContext();

        var repository =
            new EfTripInvitationDecisionRepository(
                dbContext);

        var result =
            await repository.GetByIdAsync(
                Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Accept_Should_Save_Invitation_And_Member()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var invitation =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(invitation);

        invitation.Accept(DecisionAt);

        var member =
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation);

        await fixture.Repository.SaveDecisionAsync(
            invitation,
            member);

        dbContext.ChangeTracker.Clear();

        var storedInvitation =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        var storedMember =
            await fixture.Repository
                .GetMemberByInvitationIdAsync(
                    fixture.Invitation.Id);

        Assert.NotNull(storedInvitation);
        Assert.NotNull(storedMember);

        Assert.Equal(
            TripInvitationStatus.Accepted,
            storedInvitation.Status);

        Assert.Equal(
            DecisionAt,
            storedInvitation.RespondedAtUtc);

        Assert.Equal(
            fixture.Invitation.Id,
            storedMember.InvitationId);

        Assert.Equal(
            fixture.Invitation.TripPlanId,
            storedMember.TripPlanId);

        Assert.Equal(
            fixture.Invitation.RecipientUserId,
            storedMember.MemberUserId);

        Assert.True(
            storedMember.IsActive);
    }

    [Fact]
    public async Task Accept_Without_Member_Should_Fail()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var invitation =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(invitation);

        invitation.Accept(DecisionAt);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                fixture.Repository.SaveDecisionAsync(
                    invitation,
                    null));

        dbContext.ChangeTracker.Clear();

        var stored =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Fact]
    public async Task Reject_Should_Save_Status_Without_Member()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var invitation =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(invitation);

        invitation.Reject(DecisionAt);

        await fixture.Repository.SaveDecisionAsync(
            invitation,
            null);

        dbContext.ChangeTracker.Clear();

        var stored =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        var member =
            await fixture.Repository
                .GetMemberByInvitationIdAsync(
                    fixture.Invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Rejected,
            stored.Status);

        Assert.Equal(
            DecisionAt,
            stored.RespondedAtUtc);

        Assert.Null(member);
    }

    [Fact]
    public async Task Cancel_Should_Save_Status_Without_Member()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var invitation =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(invitation);

        invitation.Cancel(DecisionAt);

        await fixture.Repository.SaveDecisionAsync(
            invitation,
            null);

        dbContext.ChangeTracker.Clear();

        var stored =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Cancelled,
            stored.Status);

        Assert.Equal(
            DecisionAt,
            stored.RespondedAtUtc);
    }

    [Fact]
    public async Task Repeated_Decision_Should_Not_Create_Duplicate_Member()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var first =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        var second =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(first);
        Assert.NotNull(second);

        first.Accept(DecisionAt);
        second.Accept(DecisionAt);

        var firstMember =
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                first);

        var secondMember =
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                second);

        await fixture.Repository.SaveDecisionAsync(
            first,
            firstMember);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                fixture.Repository.SaveDecisionAsync(
                    second,
                    secondMember));

        dbContext.ChangeTracker.Clear();

        Assert.Equal(
            1,
            await dbContext.TripMembers.CountAsync());

        var storedMember =
            await fixture.Repository
                .GetMemberByInvitationIdAsync(
                    fixture.Invitation.Id);

        Assert.NotNull(storedMember);

        Assert.Equal(
            firstMember.Id,
            storedMember.Id);
    }

    [Fact]
    public async Task Saving_Unknown_Invitation_Should_Fail()
    {
        await using var dbContext = CreateDbContext();

        var repository =
            new EfTripInvitationDecisionRepository(
                dbContext);

        var invitation = CreateInvitation();

        invitation.Accept(DecisionAt);

        var member =
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () =>
                repository.SaveDecisionAsync(
                    invitation,
                    member));
    }

    [Fact]
    public async Task Accepted_Membership_Should_Not_Create_Gps_Consent()
    {
        var fixture = await CreateFixtureAsync();

        await using var dbContext = fixture.DbContext;

        var invitation =
            await fixture.Repository.GetByIdAsync(
                fixture.Invitation.Id);

        Assert.NotNull(invitation);

        invitation.Accept(DecisionAt);

        var member =
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation);

        await fixture.Repository.SaveDecisionAsync(
            invitation,
            member);

        Assert.Equal(
            0,
            await dbContext.TripGpsConsents.CountAsync());
    }
}
