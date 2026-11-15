
using System;

namespace Wayhunter.Domain.Modules.Games.Services;

/// <summary>
/// M5 Games - Server-side attempt deadline rules.
///
/// Uses trusted server UTC timestamps.
/// Does not depend on the player's device clock.
///
/// A separate per-stage timeout is not defined here.
/// </summary>
public static class GameAttemptDeadlineRules
{
    public static DateTimeOffset CalculateDeadlineUtc(
        DateTimeOffset startedAtUtc,
        int durationMinutes,
        DateTimeOffset gameExpiresAtUtc)
    {
        ValidateUtc(startedAtUtc, nameof(startedAtUtc));
        ValidateUtc(gameExpiresAtUtc, nameof(gameExpiresAtUtc));

        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                "Game duration must be positive.");
        }

        if (gameExpiresAtUtc <= startedAtUtc)
        {
            throw new InvalidOperationException(
                "Game has already expired.");
        }

        TimeSpan requestedDuration =
            TimeSpan.FromMinutes(durationMinutes);

        TimeSpan remainingDuration =
            gameExpiresAtUtc - startedAtUtc;

        // An attempt must never continue
        // beyond the published Game expiry.
        TimeSpan allowedDuration =
            requestedDuration <= remainingDuration
                ? requestedDuration
                : remainingDuration;

        return startedAtUtc.Add(allowedDuration);
    }

    public static void EnsureActive(
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineUtc,
        DateTimeOffset nowUtc)
    {
        ValidateUtc(startedAtUtc, nameof(startedAtUtc));
        ValidateUtc(deadlineUtc, nameof(deadlineUtc));
        ValidateUtc(nowUtc, nameof(nowUtc));

        if (deadlineUtc <= startedAtUtc)
        {
            throw new InvalidOperationException(
                "Invalid Game attempt deadline.");
        }

        if (nowUtc < startedAtUtc)
        {
            throw new InvalidOperationException(
                "Game attempt has not started yet.");
        }

        // Exact deadline is already expired.
        if (nowUtc >= deadlineUtc)
        {
            throw new InvalidOperationException(
                "Game attempt deadline has expired.");
        }
    }

    private static void ValidateUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default ||
            value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC timestamp is required.",
                parameterName);
        }
    }
}
