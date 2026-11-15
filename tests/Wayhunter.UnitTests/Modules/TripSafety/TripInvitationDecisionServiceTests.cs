
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripInvitationDecisionServiceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DecisionAt =
        CreatedAt.AddMinutes(10);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return DecisionAt;
        }
    }

    private static TripInvitation CreateInvitation()
    {
        return TripInvitation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-owner",
            "user-friend",
            CreatedAt);
    }

    private static TripInvitationDecisionService CreateService(
        InMemoryTripInvitationDecisionRepository repository)
    {
        return new TripInvitationDecisionService(
            repository,
            new FixedTimeProvider());
    }

    [Fact]
    public void Null_Repository_Should_Fail()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TripInvitationDecisionService(
                null!,
                new FixedTimeProvider()));
    }

    [Fact]
    public void Null_TimeProvider_Should_Fail()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        Assert.Throws<ArgumentNullException>(() =>
            new TripInvitationDecisionService(
                repository,
                null!));
    }

    [Fact]
    public async Task Accept_Should_Create_Membership()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var member = await service.AcceptAsync(
            invitation.Id,
            "user-friend");

        Assert.NotNull(member);
        Assert.True(member.IsActive);

        Assert.Equal(
            invitation.TripPlanId,
            member.TripPlanId);

        Assert.Equal(
            invitation.Id,
            member.InvitationId);

        Assert.Equal(
            "user-friend",
            member.MemberUserId);

        Assert.Equal(
            DecisionAt,
            member.JoinedAtUtc);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Accepted,
            stored.Status);

        Assert.Equal(
            DecisionAt,
            stored.RespondedAtUtc);
    }

    [Fact]
    public async Task Repeated_Accept_Should_Return_Same_Member()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var first = await service.AcceptAsync(
            invitation.Id,
            "user-friend");

        var second = await service.AcceptAsync(
            invitation.Id,
            "user-friend");

        Assert.Equal(first.Id, second.Id);

        Assert.Equal(
            first.InvitationId,
            second.InvitationId);

        Assert.Equal(
            first.JoinedAtUtc,
            second.JoinedAtUtc);
    }

    [Fact]
    public async Task Only_Recipient_Can_Accept()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AcceptAsync(
                invitation.Id,
                "user-owner"));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Accept_Without_Authentication_Should_Fail(
        string? userId)
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AcceptAsync(
                invitation.Id,
                userId));
    }

    [Fact]
    public async Task Accept_Empty_InvitationId_Should_Fail()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AcceptAsync(
                Guid.Empty,
                "user-friend"));
    }

    [Fact]
    public async Task Accept_Unknown_Invitation_Should_Fail()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var service = CreateService(repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.AcceptAsync(
                Guid.NewGuid(),
                "user-friend"));
    }

    [Fact]
    public async Task Rejected_Invitation_Cannot_Be_Accepted()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await service.RejectAsync(
            invitation.Id,
            "user-friend");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(
                invitation.Id,
                "user-friend"));

        Assert.Null(
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id));
    }

    [Fact]
    public async Task Cancelled_Invitation_Cannot_Be_Accepted()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await service.CancelAsync(
            invitation.Id,
            "user-owner");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(
                invitation.Id,
                "user-friend"));
    }

    [Fact]
    public async Task Reject_Should_Update_Status()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var result = await service.RejectAsync(
            invitation.Id,
            "user-friend");

        Assert.Equal(
            TripInvitationStatus.Rejected,
            result.Status);

        Assert.Equal(
            DecisionAt,
            result.RespondedAtUtc);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Rejected,
            stored.Status);

        Assert.Null(
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id));
    }

    [Fact]
    public async Task Repeated_Reject_Should_Be_Idempotent()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var first = await service.RejectAsync(
            invitation.Id,
            "user-friend");

        var second = await service.RejectAsync(
            invitation.Id,
            "user-friend");

        Assert.Equal(
            TripInvitationStatus.Rejected,
            second.Status);

        Assert.Equal(
            first.RespondedAtUtc,
            second.RespondedAtUtc);
    }

    [Fact]
    public async Task Only_Recipient_Can_Reject()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RejectAsync(
                invitation.Id,
                "user-owner"));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Fact]
    public async Task Accepted_Invitation_Cannot_Be_Rejected()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await service.AcceptAsync(
            invitation.Id,
            "user-friend");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RejectAsync(
                invitation.Id,
                "user-friend"));
    }

    [Fact]
    public async Task Cancel_Should_Update_Status()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var result = await service.CancelAsync(
            invitation.Id,
            "user-owner");

        Assert.Equal(
            TripInvitationStatus.Cancelled,
            result.Status);

        Assert.Equal(
            DecisionAt,
            result.RespondedAtUtc);

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Cancelled,
            stored.Status);

        Assert.Null(
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id));
    }

    [Fact]
    public async Task Repeated_Cancel_Should_Be_Idempotent()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var first = await service.CancelAsync(
            invitation.Id,
            "user-owner");

        var second = await service.CancelAsync(
            invitation.Id,
            "user-owner");

        Assert.Equal(
            TripInvitationStatus.Cancelled,
            second.Status);

        Assert.Equal(
            first.RespondedAtUtc,
            second.RespondedAtUtc);
    }

    [Fact]
    public async Task Only_Sender_Can_Cancel()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CancelAsync(
                invitation.Id,
                "user-friend"));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Fact]
    public async Task Accepted_Invitation_Cannot_Be_Cancelled()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await service.AcceptAsync(
            invitation.Id,
            "user-friend");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CancelAsync(
                invitation.Id,
                "user-owner"));
    }

    [Fact]
    public async Task Rejected_Invitation_Cannot_Be_Cancelled()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        await service.RejectAsync(
            invitation.Id,
            "user-friend");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CancelAsync(
                invitation.Id,
                "user-owner"));
    }

    [Fact]
    public async Task Authenticated_UserId_Should_Be_Trimmed()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var member = await service.AcceptAsync(
            invitation.Id,
            "  user-friend  ");

        Assert.Equal(
            "user-friend",
            member.MemberUserId);
    }

    [Fact]
    public async Task Accept_Should_Not_Automatically_Grant_Gps()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(repository);

        var member = await service.AcceptAsync(
            invitation.Id,
            "user-friend");

        // GPS sharing is a separate, explicit opt-in.
        // Accepting a trip invitation cannot grant it.

        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            member.MemberUserId);

        Assert.True(member.IsActive);
        Assert.False(consent.IsGranted);
    }
}
