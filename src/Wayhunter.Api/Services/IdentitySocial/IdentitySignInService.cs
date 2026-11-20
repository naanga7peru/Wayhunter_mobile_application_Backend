using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed record IdentitySignInResult(
    string UserId,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed class IdentitySignInService
{
    private readonly WayhunterSharedDbContext _db;
    private readonly LoginAccountLookupService _lookup;
    private readonly LoginCredentialValidationService _credentials;
    private readonly VerifiedSessionCreationService _sessions;
    private readonly SignedSessionTokenService _tokens;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;

    public IdentitySignInService(
        WayhunterSharedDbContext db,
        LoginAccountLookupService lookup,
        LoginCredentialValidationService credentials,
        VerifiedSessionCreationService sessions,
        SignedSessionTokenService tokens,
        IConfiguration configuration,
        TimeProvider clock)
    {
        _db = db;
        _lookup = lookup;
        _credentials = credentials;
        _sessions = sessions;
        _tokens = tokens;
        _configuration = configuration;
        _clock = clock;
    }

    public async Task<IdentitySignInResult?> SignInAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = await _lookup.FindByEmailAsync(email, cancellationToken);
        if (!_credentials.Validate(account, password) ||
            account is null || account.DeletionRequestedAtUtc.HasValue)
        {
            return null;
        }

        var lifetimeMinutes = _configuration.GetValue<int?>(
            "Identity:SessionLifetimeMinutes");
        if (!lifetimeMinutes.HasValue || lifetimeMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "Identity:SessionLifetimeMinutes must be configured from 1 to 1440.");
        }

        var now = _clock.GetUtcNow();
        var session = _sessions.Create(
            account,
            now,
            now.AddMinutes(lifetimeMinutes.Value));
        _db.IdentitySessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return new IdentitySignInResult(
            account.UserId,
            _tokens.Issue(account, session, now),
            session.ExpiresAtUtc);
    }
}
