
using Wayhunter.Application.Modules.IdentitySocial.Services;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed record LoginAuthenticationResult(
    bool IsAuthenticated,
    bool IsLocked,
    TimeSpan RetryAfter,
    UserAccount? Account,
    LoginLockoutState NextLockoutState);

public sealed class LoginAuthenticationService
{
    private readonly LoginAccountLookupService _lookup;
    private readonly LoginCredentialValidationService _credentials;

    public LoginAuthenticationService(
        LoginAccountLookupService lookup,
        LoginCredentialValidationService credentials)
    {
        _lookup = lookup ??
            throw new ArgumentNullException(nameof(lookup));

        _credentials = credentials ??
            throw new ArgumentNullException(nameof(credentials));
    }

    public async Task<LoginAuthenticationResult> AuthenticateAsync(
        string? email,
        string? password,
        LoginLockoutState currentState,
        LoginLockoutOptions options,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        if (options.MaxFailedAttempts <= 0 ||
            options.LockoutDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options));
        }

        // Validate time and check lockout before credentials.
        var decision = LoginLockoutPolicy.Check(
            currentState,
            nowUtc);

        if (!decision.IsAllowed)
        {
            return new LoginAuthenticationResult(
                false,
                true,
                decision.RetryAfter,
                null,
                currentState);
        }

        // The state is supplied by the caller.
        // No persistence happens in this service.
        var account = await _lookup.FindByEmailAsync(
            email,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (!_credentials.Validate(account, password))
        {
            // Failed attempts for a real account must be
            // persisted atomically by a future storage layer.
            // Missing accounts have no account-specific state.
            if (account is null)
            {
                return new LoginAuthenticationResult(
                    false,
                    false,
                    TimeSpan.Zero,
                    null,
                    currentState);
            }

            var nextState = LoginLockoutPolicy.RecordFailure(
                currentState,
                options,
                nowUtc);

            var nextDecision = LoginLockoutPolicy.Check(
                nextState,
                nowUtc);

            return new LoginAuthenticationResult(
                false,
                !nextDecision.IsAllowed,
                nextDecision.RetryAfter,
                null,
                nextState);
        }

        var resetState = LoginLockoutPolicy.RecordSuccess(
            currentState,
            nowUtc);

        return new LoginAuthenticationResult(
            true,
            false,
            TimeSpan.Zero,
            account,
            resetState);
    }
}
