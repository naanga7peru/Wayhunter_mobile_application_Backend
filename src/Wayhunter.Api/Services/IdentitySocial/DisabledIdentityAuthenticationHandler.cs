
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class DisabledIdentityAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DisabledIdentity";

    public DisabledIdentityAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        // Authentication disabled:
        // Never accept credentials or create a principal.
        return Task.FromResult(
            AuthenticateResult.NoResult());
    }

    protected override Task HandleChallengeAsync(
        AuthenticationProperties properties)
    {
        // Protected endpoints must return 401, never 500.
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}
