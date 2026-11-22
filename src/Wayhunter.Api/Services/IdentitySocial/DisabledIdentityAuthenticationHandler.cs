
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Contracts.Responses;

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
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(ApiErrorResponse.Create(
            "authentication_required",
            "Authentication is required.",
            Context.TraceIdentifier));
    }

    protected override Task HandleForbiddenAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Response.WriteAsJsonAsync(ApiErrorResponse.Create(
            "access_denied",
            "The authenticated account is not allowed to access this resource.",
            Context.TraceIdentifier));
    }
}
