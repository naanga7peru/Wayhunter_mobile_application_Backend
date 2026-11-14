
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripGpsConsentTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static TripGpsConsent CreateConsent()
    {
        return TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            "user-001");
    }

    [Fact]
    public void New_Consent_Should_Be_Disabled()
    {
        var consent = CreateConsent();

        Assert.False(consent.IsGranted);
        Assert.Null(consent.GrantedAtUtc);
        Assert.Null(consent.RevokedAtUtc);
    }

    [Fact]
    public void Create_Should_Trim_UserId()
    {
        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            "  user-001  ");

        Assert.Equal("user-001", consent.UserId);
    }

    [Fact]
    public void Empty_SessionId_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            TripGpsConsent.CreateDisabled(
                Guid.Empty,
                "user-001"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_UserId_Should_Fail(string userId)
    {
        Assert.Throws<ArgumentException>(() =>
            TripGpsConsent.CreateDisabled(
                Guid.NewGuid(),
                userId));
    }

    [Fact]
    public void Grant_Should_Enable_Gps_Consent()
    {
        var consent = CreateConsent();

        consent.Grant(StartTime);

        Assert.True(consent.IsGranted);
        Assert.Equal(StartTime, consent.GrantedAtUtc);
        Assert.Null(consent.RevokedAtUtc);
    }

    [Fact]
    public void Duplicate_Grant_Should_Not_Change_Time()
    {
        var consent = CreateConsent();

        consent.Grant(StartTime);
        consent.Grant(StartTime.AddMinutes(10));

        Assert.True(consent.IsGranted);
        Assert.Equal(StartTime, consent.GrantedAtUtc);
    }

    [Fact]
    public void Revoke_Should_Disable_Gps_Consent()
    {
        var consent = CreateConsent();

        consent.Grant(StartTime);

        var revokeTime = StartTime.AddMinutes(5);

        consent.Revoke(revokeTime);

        Assert.False(consent.IsGranted);
        Assert.Equal(revokeTime, consent.RevokedAtUtc);
    }

    [Fact]
    public void Revoke_Without_Grant_Should_Do_Nothing()
    {
        var consent = CreateConsent();

        consent.Revoke(StartTime);

        Assert.False(consent.IsGranted);
        Assert.Null(consent.RevokedAtUtc);
    }

    [Fact]
    public void Duplicate_Revoke_Should_Keep_First_Time()
    {
        var consent = CreateConsent();

        consent.Grant(StartTime);
        consent.Revoke(StartTime.AddMinutes(5));

        consent.Revoke(StartTime.AddMinutes(10));

        Assert.False(consent.IsGranted);

        Assert.Equal(
            StartTime.AddMinutes(5),
            consent.RevokedAtUtc);
    }

    [Fact]
    public void ReGrant_Should_Clear_Revocation_Time()
    {
        var consent = CreateConsent();

        consent.Grant(StartTime);
        consent.Revoke(StartTime.AddMinutes(5));

        var newGrantTime = StartTime.AddMinutes(10);

        consent.Grant(newGrantTime);

        Assert.True(consent.IsGranted);
        Assert.Equal(newGrantTime, consent.GrantedAtUtc);
        Assert.Null(consent.RevokedAtUtc);
    }

    [Fact]
    public void Revoke_Before_Grant_Time_Should_Fail()
    {
        var consent = CreateConsent();

        consent.Grant(StartTime);

        Assert.Throws<InvalidOperationException>(() =>
            consent.Revoke(StartTime.AddMinutes(-1)));

        Assert.True(consent.IsGranted);
    }

    [Fact]
    public void Grant_Before_Revocation_Time_Should_Fail()
    {
        var consent = CreateConsent();

        consent.Grant(StartTime);
        consent.Revoke(StartTime.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() =>
            consent.Grant(StartTime.AddMinutes(5)));

        Assert.False(consent.IsGranted);
    }

    [Fact]
    public void Grant_Should_Normalize_Time_To_Utc()
    {
        var consent = CreateConsent();

        var sriLankaTime = new DateTimeOffset(
            2026, 11, 14, 13, 30, 0,
            TimeSpan.FromMinutes(330));

        consent.Grant(sriLankaTime);

        Assert.Equal(
            TimeSpan.Zero,
            consent.GrantedAtUtc!.Value.Offset);

        Assert.Equal(
            StartTime,
            consent.GrantedAtUtc);
    }

    [Fact]
    public void Invalid_Grant_Time_Should_Fail()
    {
        var consent = CreateConsent();

        Assert.Throws<ArgumentException>(() =>
            consent.Grant(default));
    }

    [Fact]
    public void Invalid_Revoke_Time_Should_Fail()
    {
        var consent = CreateConsent();

        Assert.Throws<ArgumentException>(() =>
            consent.Revoke(default));
    }
}
