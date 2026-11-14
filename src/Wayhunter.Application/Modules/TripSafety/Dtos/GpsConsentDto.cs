
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Dtos;

public sealed record GpsConsentDto(
    Guid SessionId,
    string UserId,
    bool IsGranted,
    DateTimeOffset? GrantedAtUtc,
    DateTimeOffset? RevokedAtUtc)
{
    public static GpsConsentDto FromDomain(
        TripGpsConsent consent)
    {
        ArgumentNullException.ThrowIfNull(consent);

        return new GpsConsentDto(
            consent.SessionId,
            consent.UserId,
            consent.IsGranted,
            consent.GrantedAtUtc,
            consent.RevokedAtUtc);
    }
}
