
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class InMemoryTripInvitationDecisionRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DecisionAt =
        CreatedAt.AddMinutes(5);

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

    [Fact]
    public async Task Add_Should_Store_Pending_Invitation()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);
        Assert.Equal(invitation.Id, stored.Id);
        Assert.Equal(invitation.TripPlanId, stored.TripPlanId);
        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
        Assert.Null(stored.RespondedAtUtc);
    }

    [Fact]
    public async Task GetById_Should_Return_Null_When_Not_Found()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var result = await repository.GetByIdAsync(
            Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Add_Duplicate_InvitationId_Should_Fail()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddAsync(invitation));
    }

    [Fact]
    public async Task Duplicate_Pending_Invitation_Should_Fail()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var tripPlanId = Guid.NewGuid();

        var first = CreateInvitation(
            tripPlanId: tripPlanId);

        var second = CreateInvitation(
            tripPlanId: tripPlanId);

        await repository.AddAsync(first);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddAsync(second));
    }

    [Fact]
    public async Task Different_Recipients_Should_Be_Allowed()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var tripPlanId = Guid.NewGuid();

        var first = CreateInvitation(
            tripPlanId: tripPlanId,
            recipientUserId: "user-001");

        var second = CreateInvitation(
            tripPlanId: tripPlanId,
            recipientUserId: "user-002");

        await repository.AddAsync(first);
        await repository.AddAsync(second);

        Assert.NotNull(
            await repository.GetByIdAsync(first.Id));

        Assert.NotNull(
            await repository.GetByIdAsync(second.Id));
    }

    [Fact]
    public async Task GetById_Should_Return_Separate_Instance()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);
        Assert.NotSame(invitation, stored);
    }

    [Fact]
    public async Task Changing_Loaded_Invitation_Should_Not_Auto_Save()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var loaded = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(loaded);

        loaded.Accept(DecisionAt);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);

        Assert.Null(stored.RespondedAtUtc);
    }

    [Fact]
    public async Task Accept_Should_Save_Invitation_And_Member()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var loaded = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(loaded);

        loaded.Accept(DecisionAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            loaded);

        await repository.SaveDecisionAsync(
            loaded,
            member);

        var storedInvitation =
            await repository.GetByIdAsync(invitation.Id);

        var storedMember =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(storedInvitation);
        Assert.NotNull(storedMember);

        Assert.Equal(
            TripInvitationStatus.Accepted,
            storedInvitation.Status);

        Assert.Equal(
            DecisionAt,
            storedInvitation.RespondedAtUtc);

        Assert.Equal(
            invitation.Id,
            storedMember.InvitationId);

        Assert.Equal(
            invitation.TripPlanId,
            storedMember.TripPlanId);

        Assert.Equal(
            invitation.RecipientUserId,
            storedMember.MemberUserId);

        Assert.True(storedMember.IsActive);
    }

    [Fact]
    public async Task Accept_Without_Member_Should_Fail()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        invitation.Accept(DecisionAt);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveDecisionAsync(
                invitation,
                null));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Fact]
    public async Task Repeated_Accept_Save_Should_Not_Create_Duplicate()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var first = await repository.GetByIdAsync(
            invitation.Id);

        var second = await repository.GetByIdAsync(
            invitation.Id);

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

        await repository.SaveDecisionAsync(
            first,
            firstMember);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveDecisionAsync(
                second,
                secondMember));

        var storedMember =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(storedMember);
        Assert.Equal(firstMember.Id, storedMember.Id);
    }

    [Fact]
    public async Task Reject_Should_Save_Status_Without_Member()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        invitation.Reject(DecisionAt);

        await repository.SaveDecisionAsync(
            invitation,
            null);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        var member =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Rejected,
            stored.Status);

        Assert.Null(member);
    }

    [Fact]
    public async Task Cancel_Should_Save_Status_Without_Member()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        invitation.Cancel(DecisionAt);

        await repository.SaveDecisionAsync(
            invitation,
            null);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        var member =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Cancelled,
            stored.Status);

        Assert.Null(member);
    }

    [Fact]
    public async Task Rejected_Invitation_Cannot_Add_Member()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        invitation.Accept(DecisionAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        var rejected = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(rejected);

        rejected.Reject(DecisionAt);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveDecisionAsync(
                rejected,
                member));

        Assert.Null(
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id));
    }

    [Fact]
    public async Task Saving_Unknown_Invitation_Should_Fail()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        invitation.Accept(DecisionAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(
            () => repository.SaveDecisionAsync(
                invitation,
                member));
    }

    [Fact]
    public async Task Cancelled_Invitation_Can_Be_Replaced()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var tripPlanId = Guid.NewGuid();

        var original = CreateInvitation(
            tripPlanId: tripPlanId);

        await repository.AddAsync(original);

        original.Cancel(DecisionAt);

        await repository.SaveDecisionAsync(
            original,
            null);

        var newInvitation = CreateInvitation(
            tripPlanId: tripPlanId);

        await repository.AddAsync(newInvitation);

        var stored = await repository.GetByIdAsync(
            newInvitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Fact]
    public async Task Concurrent_Decisions_Only_One_Should_Win()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var acceptCopy = await repository.GetByIdAsync(
            invitation.Id);

        var rejectCopy = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(acceptCopy);
        Assert.NotNull(rejectCopy);

        acceptCopy.Accept(DecisionAt);
        rejectCopy.Reject(DecisionAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            acceptCopy);

        var results = await Task.WhenAll(
            Record.ExceptionAsync(() =>
                repository.SaveDecisionAsync(
                    acceptCopy,
                    member)),
            Record.ExceptionAsync(() =>
                repository.SaveDecisionAsync(
                    rejectCopy,
                    null)));

        Assert.Equal(
            1,
            results.Count(x => x is null));

        Assert.Equal(
            1,
            results.Count(x =>
                x is InvalidOperationException));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        var storedMember =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(stored);

        if (stored.Status == TripInvitationStatus.Accepted)
        {
            Assert.NotNull(storedMember);
        }
        else
        {
            Assert.Equal(
                TripInvitationStatus.Rejected,
                stored.Status);

            Assert.Null(storedMember);
        }
    }

    [Fact]
    public async Task Cancelled_Operation_Should_Not_Add()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.AddAsync(
                invitation,
                cts.Token));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.Null(stored);
    }

    [Fact]
    public async Task Accepted_Membership_Should_Not_Grant_Gps()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        invitation.Accept(DecisionAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        await repository.SaveDecisionAsync(
            invitation,
            member);

        var storedMember =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(storedMember);

        // GPS consent is a separate opt-in process.
        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            storedMember.MemberUserId);

        Assert.True(storedMember.IsActive);
        Assert.False(consent.IsGranted);
    }
}
