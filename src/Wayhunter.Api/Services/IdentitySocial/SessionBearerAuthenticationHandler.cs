
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Wayhunter.Api.Contracts.Responses;
using Microsoft.Extensions.Options;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SessionBearerAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SessionBearer";

    private readonly AuthenticatedSessionAuthorizationService
        _sessionAuthorization;
    private readonly TrustedAdministratorRoleResolutionService?
        _administratorRoleResolution;

    public SessionBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AuthenticatedSessionAuthorizationService sessionAuthorization,
        TrustedAdministratorRoleResolutionService? administratorRoleResolution = null)
        : base(options, logger, encoder)
    {
        _sessionAuthorization = sessionAuthorization
            ?? throw new ArgumentNullException(
                nameof(sessionAuthorization));
        _administratorRoleResolution = administratorRoleResolution;
    }

    protected override async Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        var authorization =
            Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization))
        {
            return AuthenticateResult.NoResult();
        }

        if (!AuthenticationHeaderValue.TryParse(
                authorization, out var header) ||
            !string.Equals(
                header.Scheme,
                "Bearer",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            return AuthenticateResult.Fail(
                "Invalid bearer authorization header.");
        }

        var identity =
            await _sessionAuthorization.AuthorizeAsync(
                header.Parameter,
                Context.RequestAborted);

        if (identity is null)
        {
            return AuthenticateResult.Fail(
                "Invalid or inactive session.");
        }

        var claims = new List<Claim>
        {
            new Claim("sub", identity.UserId),
            new Claim("sid", identity.SessionId),
            new Claim(
                ClaimTypes.NameIdentifier,
                identity.UserId)
        };

        // Elevated access is derived only from the trusted, persisted
        // role assignment after the bearer token and session validate.
        // An unavailable role reader produces no claim and remains fail-closed.
        if (_administratorRoleResolution is not null)
        {
            var administratorRole = await _administratorRoleResolution
                .ResolveAdminRoleClaimAsync(
                    header.Parameter,
                    Context.RequestAborted);

            if (administratorRole is not null)
                claims.Add(administratorRole);
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                SchemeName));

        return AuthenticateResult.Success(
            new AuthenticationTicket(
                principal,
                SchemeName));
    }

    protected override Task HandleChallengeAsync(
        AuthenticationProperties properties) =>
        WriteAuthorizationErrorAsync(
            StatusCodes.Status401Unauthorized,
            "authentication_required",
            "Authentication is required.");

    protected override Task HandleForbiddenAsync(
        AuthenticationProperties properties) =>
        WriteAuthorizationErrorAsync(
            StatusCodes.Status403Forbidden,
            "access_denied",
            "The authenticated account is not allowed to access this resource.");

    private Task WriteAuthorizationErrorAsync(
        int statusCode,
        string code,
        string message)
    {
        Response.StatusCode = statusCode;
        return Response.WriteAsJsonAsync(ApiErrorResponse.Create(
            code, message, Context.TraceIdentifier));
    }
}
