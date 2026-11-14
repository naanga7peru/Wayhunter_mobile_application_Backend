
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Dtos;

public sealed record TripSessionDto(
    Guid SessionId,
    string TripPlanId,
    LiveTripStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc)
{
    public static TripSessionDto FromDomain(
        LiveTripSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new TripSessionDto(
            session.SessionId,
            session.TripPlanId,
            session.Status,
            session.StartedAtUtc,
            session.FinishedAtUtc);
    }
}
