
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Services.IdentitySocial;

namespace Wayhunter.Api.Extensions;

public static class SessionAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddWayhunterSessionAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var issuer = configuration["Authentication:Jwt:Issuer"];
        var audience = configuration["Authentication:Jwt:Audience"];
        var secret = configuration["Authentication:Jwt:SigningSecret"];

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException(
                "JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "JWT audience is not configured.");
        }

        if (string.IsNullOrWhiteSpace(secret) ||
            Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing secret must contain at least 32 UTF-8 bytes.");
        }

        services.AddSingleton(
            new SignedSessionTokenService(
                issuer,
                audience,
                secret));

        services.AddScoped<
            AuthenticatedSessionAuthorizationService>();

        services
            .AddAuthentication(
                SessionBearerAuthenticationHandler.SchemeName)
            .AddScheme<
                AuthenticationSchemeOptions,
                SessionBearerAuthenticationHandler>(
                SessionBearerAuthenticationHandler.SchemeName,
                _ => { });

        services.AddAuthorization();

        // IMPORTANT:
        // IAuthenticatedSessionReader must be registered
        // separately using trusted server-side persistence.
        //
        // Do not use client-provided account/session data.
        // Do not activate production middleware until
        // this dependency has a working implementation.

        return services;
    }
}
