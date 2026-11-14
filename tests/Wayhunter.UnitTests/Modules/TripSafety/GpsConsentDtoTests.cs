
using System;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class GpsConsentDtoTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Disabled_Consent_Should_Map_Correctly()
    {
        var sessionId = Guid.NewGuid();

        var consent = TripGpsConsent.CreateDisabled(
            sessionId,
            "user-001");

        var dto = GpsConsentDto.FromDomain(consent);

        Assert.Equal(sessionId, dto.SessionId);
        Assert.Equal("user-001", dto.UserId);
        Assert.False(dto.IsGranted);
        Assert.Null(dto.GrantedAtUtc);
        Assert.Null(dto.RevokedAtUtc);
    }

    [Fact]
    public void Granted_Consent_Should_Map_Correctly()
    {
        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            "user-001");

        consent.Grant(TestTime);

        var dto = GpsConsentDto.FromDomain(consent);

        Assert.True(dto.IsGranted);
        Assert.Equal(TestTime, dto.GrantedAtUtc);
        Assert.Null(dto.RevokedAtUtc);
    }

    [Fact]
    public void Revoked_Consent_Should_Map_Correctly()
    {
        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            "user-001");

        consent.Grant(TestTime);

        var revokeTime = TestTime.AddMinutes(5);

        consent.Revoke(revokeTime);

        var dto = GpsConsentDto.FromDomain(consent);

        Assert.False(dto.IsGranted);
        Assert.Equal(TestTime, dto.GrantedAtUtc);
        Assert.Equal(revokeTime, dto.RevokedAtUtc);
    }

    [Fact]
    public void Null_Domain_Should_Be_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GpsConsentDto.FromDomain(null!));
    }
}
