
namespace Wayhunter.Application.Modules.IdentitySocial.Services;

public sealed record LoginLockoutOptions(
    int MaxFailedAttempts,
    TimeSpan LockoutDuration);

public sealed record LoginLockoutState(
    int FailedAttempts,
    DateTimeOffset? LockedUntilUtc);

public sealed record LoginLockoutDecision(
    bool IsAllowed,
    TimeSpan RetryAfter);

public static class LoginLockoutPolicy
{
    public static LoginLockoutDecision Check(
        LoginLockoutState state,
        DateTimeOffset nowUtc)
    {
        ValidateState(state);
        ValidateUtc(nowUtc);

        if (state.LockedUntilUtc is null ||
            nowUtc >= state.LockedUntilUtc.Value)
        {
            return new LoginLockoutDecision(
                true,
                TimeSpan.Zero);
        }

        return new LoginLockoutDecision(
            false,
            state.LockedUntilUtc.Value - nowUtc);
    }

    public static LoginLockoutState RecordFailure(
        LoginLockoutState state,
        LoginLockoutOptions options,
        DateTimeOffset nowUtc)
    {
        ValidateState(state);
        ValidateOptions(options);
        ValidateUtc(nowUtc);

        var decision = Check(state, nowUtc);

        // Do not extend an existing lockout
        // when another attempt arrives.
        if (!decision.IsAllowed)
            return state;

        // Expired lockout starts a fresh failure cycle.
        var failedAttempts =
            state.LockedUntilUtc.HasValue
                ? 0
                : state.FailedAttempts;

        failedAttempts = checked(failedAttempts + 1);

        if (failedAttempts >= options.MaxFailedAttempts)
        {
            return new LoginLockoutState(
                failedAttempts,
                nowUtc.Add(options.LockoutDuration));
        }

        return new LoginLockoutState(
            failedAttempts,
            null);
    }

    // Call only after successful trusted authentication.
    public static LoginLockoutState RecordSuccess(
        LoginLockoutState state,
        DateTimeOffset nowUtc)
    {
        ValidateState(state);
        ValidateUtc(nowUtc);

        if (!Check(state, nowUtc).IsAllowed)
        {
            throw new InvalidOperationException(
                "Cannot reset an active login lockout.");
        }

        return new LoginLockoutState(0, null);
    }

    private static void ValidateOptions(
        LoginLockoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxFailedAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxFailedAttempts must be positive.");
        }

        if (options.LockoutDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "LockoutDuration must be positive.");
        }
    }

    private static void ValidateState(
        LoginLockoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.FailedAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "FailedAttempts cannot be negative.");
        }

        if (state.LockedUntilUtc.HasValue)
        {
            ValidateUtc(state.LockedUntilUtc.Value);
        }
    }

    private static void ValidateUtc(DateTimeOffset value)
    {
        if (value == default ||
            value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A valid UTC timestamp is required.");
        }
    }
}
