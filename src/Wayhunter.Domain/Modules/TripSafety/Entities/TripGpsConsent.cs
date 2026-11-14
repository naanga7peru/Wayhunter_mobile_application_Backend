
using System;

namespace Wayhunter.Domain.Modules.TripSafety.Entities;

public sealed class TripGpsConsent
{
    public Guid SessionId { get; }

    public string UserId { get; }

    public bool IsGranted { get; private set; }

    public DateTimeOffset? GrantedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private TripGpsConsent(Guid sessionId, string userId)
    {
        SessionId = sessionId;
        UserId = userId;
        IsGranted = false;
    }

    public static TripGpsConsent CreateDisabled(
        Guid sessionId,
        string userId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Session ID is required.",
                nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        return new TripGpsConsent(
            sessionId,
            userId.Trim());
    }

    // Call only after an explicit, authenticated
    // user consent action. Inviting or accepting
    // a trip must never call this automatically.
    public void Grant(DateTimeOffset serverTimeUtc)
    {
        var time = ValidateTime(serverTimeUtc);

        if (IsGranted)
        {
            return;
        }

        if (RevokedAtUtc.HasValue &&
            time < RevokedAtUtc.Value)
        {
            throw new InvalidOperationException(
                "Consent cannot be granted before revocation time.");
        }

        IsGranted = true;
        GrantedAtUtc = time;
        RevokedAtUtc = null;
    }

    // Revoke consent and stop future publishing.
    public void Revoke(DateTimeOffset serverTimeUtc)
    {
        var time = ValidateTime(serverTimeUtc);

        if (!IsGranted)
        {
            return;
        }

        if (GrantedAtUtc.HasValue &&
            time < GrantedAtUtc.Value)
        {
            throw new InvalidOperationException(
                "Revocation cannot happen before consent.");
        }

        IsGranted = false;
        RevokedAtUtc = time;
    }

    private static DateTimeOffset ValidateTime(
        DateTimeOffset serverTimeUtc)
    {
        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid server time is required.",
                nameof(serverTimeUtc));
        }

        return serverTimeUtc.ToUniversalTime();
    }
}
