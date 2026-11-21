using System.Net.Http.Headers;
using System.Security.Claims;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.TripSafety.Services;

namespace Wayhunter.Api.Extensions;

public static class AdminSafetyServiceCollectionExtensions
{
    public const string Policy = "M3AdminSafetyMonitoring";

    public static IServiceCollection AddAdminSafetyMonitoring(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(async context => context.Resource is HttpContext http &&
                await IsTrustedAdminAsync(http, context.User));
        }));

        services.AddScoped<AdminSafetyMonitoringService>();
        return services;
    }

    internal static async Task<bool> IsTrustedAdminAsync(HttpContext http, ClaimsPrincipal principal)
    {
        if (principal.Identity?.AuthenticationType != SessionBearerAuthenticationHandler.SchemeName ||
            !TrustedIdentityAccessContract.TryResolveUserId(principal, out _) ||
            http.Request.Headers.Authorization.Count != 1 ||
            !AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var resolver = http.RequestServices.GetService<TrustedAdministratorRoleResolutionService>();
            var role = resolver is null ? null :
                await resolver.ResolveAdminRoleClaimAsync(header.Parameter, http.RequestAborted);
            return role is { Type: ClaimTypes.Role, Value: "Admin" };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException && !http.RequestAborted.IsCancellationRequested)
        {
            return false;
        }
    }
}
