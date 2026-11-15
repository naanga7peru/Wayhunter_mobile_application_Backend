
using System;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Events;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class VerifiedAccountActivatedEventTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount CreatePendingAccount()
    {
        return UserAccount.RegisterPending(
            "Demo Traveler",
            "traveler@example.com",
            "test-only-password-hash",
            Now);
    }

    private static UserAccount CreateVerifiedAccount()
    {
        var account = CreatePendingAccount();

        account.ActivateAfterVerifiedOtp(
            Now.AddMinutes(5));

        return account;
    }

    [Fact]
    public void VerifiedAccount_ShouldCreateActivationEvent()
    {
        var account = CreateVerifiedAccount();
        var correlationId = Guid.NewGuid();

        var result =
            VerifiedAccountActivatedEvent
                .CreateFromVerifiedAccount(
                    account,
                    correlationId);

        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.Equal(account.UserId, result.UserId);
        Assert.Equal(account.VerifiedAtUtc, result.VerifiedAtUtc);
        Assert.Equal(correlationId, result.CorrelationId);
    }

    [Fact]
    public void PendingAccount_ShouldBeRejected()
    {
        var account = CreatePendingAccount();

        Assert.Throws<InvalidOperationException>(() =>
            VerifiedAccountActivatedEvent
                .CreateFromVerifiedAccount(
                    account,
                    Guid.NewGuid()));
    }

    [Fact]
    public void SuspendedAccount_ShouldBeRejected()
    {
        var account = CreateVerifiedAccount();

        account.Suspend();

        Assert.Throws<InvalidOperationException>(() =>
            VerifiedAccountActivatedEvent
                .CreateFromVerifiedAccount(
                    account,
                    Guid.NewGuid()));
    }

    [Fact]
    public void EmptyCorrelationId_ShouldBeRejected()
    {
        var account = CreateVerifiedAccount();

        Assert.Throws<ArgumentException>(() =>
            VerifiedAccountActivatedEvent
                .CreateFromVerifiedAccount(
                    account,
                    Guid.Empty));
    }

    [Fact]
    public void NullAccount_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            VerifiedAccountActivatedEvent
                .CreateFromVerifiedAccount(
                    null!,
                    Guid.NewGuid()));
    }

    [Fact]
    public void ChangingDisplayName_ShouldPreserveUserId()
    {
        var account = CreateVerifiedAccount();
        var originalUserId = account.UserId;

        account.UpdateDisplayName("Updated Traveler");

        var result =
            VerifiedAccountActivatedEvent
                .CreateFromVerifiedAccount(
                    account,
                    Guid.NewGuid());

        Assert.Equal(originalUserId, result.UserId);
    }
}
