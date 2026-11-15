
namespace Wayhunter.Application.Modules.IdentitySocial.Services;

public sealed record LoginLockoutUpdateResult(
    LoginLockoutState State,
    bool IsAllowed,
    TimeSpan RetryAfter);

public sealed class LoginLockoutStateCoordinator
{
    private const int MaxRetries = 5;

    private readonly ILoginLockoutStateStore _store;

    public LoginLockoutStateCoordinator(
        ILoginLockoutStateStore store)
    {
        _store = store ??
            throw new ArgumentNullException(nameof(store));
    }

    public async Task<LoginLockoutUpdateResult> RecordFailureAsync(
        string userId,
        LoginLockoutOptions options,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(options);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await _store.ReadAsync(
                userId, cancellationToken);

            ValidateSnapshot(snapshot);

            var currentDecision = LoginLockoutPolicy.Check(
                snapshot.State, nowUtc);

            if (!currentDecision.IsAllowed)
            {
                return new LoginLockoutUpdateResult(
                    snapshot.State,
                    false,
                    currentDecision.RetryAfter);
            }

            var nextState = LoginLockoutPolicy.RecordFailure(
                snapshot.State, options, nowUtc);

            var saved = await _store.TryWriteAsync(
                userId,
                snapshot.Version,
                nextState,
                cancellationToken);

            if (!saved)
                continue;

            var nextDecision = LoginLockoutPolicy.Check(
                nextState, nowUtc);

            return new LoginLockoutUpdateResult(
                nextState,
                nextDecision.IsAllowed,
                nextDecision.RetryAfter);
        }

        throw new InvalidOperationException(
            "Concurrent login lockout update failed.");
    }

    public async Task<LoginLockoutUpdateResult> RecordSuccessAsync(
        string userId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await _store.ReadAsync(
                userId, cancellationToken);

            ValidateSnapshot(snapshot);

            var nextState = LoginLockoutPolicy.RecordSuccess(
                snapshot.State, nowUtc);

            var saved = await _store.TryWriteAsync(
                userId,
                snapshot.Version,
                nextState,
                cancellationToken);

            if (!saved)
                continue;

            return new LoginLockoutUpdateResult(
                nextState,
                true,
                TimeSpan.Zero);
        }

        throw new InvalidOperationException(
            "Concurrent login lockout reset failed.");
    }

    public async Task<LoginLockoutDecision> CheckAsync(
        string userId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = await _store.ReadAsync(
            userId, cancellationToken);

        ValidateSnapshot(snapshot);

        return LoginLockoutPolicy.Check(
            snapshot.State, nowUtc);
    }

    private static void ValidateUserId(string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out var parsed) ||
            parsed == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid account UserId is required.",
                nameof(userId));
        }
    }

    private static void ValidateSnapshot(
        LoginLockoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.State is null ||
            snapshot.Version < 0)
        {
            throw new InvalidOperationException(
                "Invalid login lockout storage snapshot.");
        }
    }
}
