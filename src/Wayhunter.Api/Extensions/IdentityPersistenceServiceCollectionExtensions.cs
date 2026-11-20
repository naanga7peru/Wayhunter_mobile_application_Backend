
using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.IdentitySocial.Services;

namespace Wayhunter.Api.Extensions;

public static class IdentityPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddWayhunterIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // M1 uses the single shared database context.
        services.AddWayhunterSharedPersistence(configuration);

        services.TryAddScoped<
            IPasswordHasher<object>, PasswordHasher<object>>();
        services.TryAddScoped<RegistrationPreparationService>();
        services.TryAddScoped<LoginAccountLookupService>();
        services.TryAddScoped<LoginCredentialValidationService>();
        services.TryAddScoped<VerifiedSessionCreationService>();
        services.TryAddScoped<IdentityRegistrationOtpService>();
        services.TryAddScoped<IdentitySignInService>();
        services.TryAddScoped<PasswordRecoveryService>();
        services.TryAddScoped<ILoginAccountReader, SqlLoginAccountReader>();
        services.TryAddSingleton<
            IIdentityOtpCodeGenerator,
            CryptographicIdentityOtpCodeGenerator>();
        services.TryAddSingleton(_ => new IdentityOtpVerifier(
            configuration["Authentication:Jwt:SigningSecret"] ?? string.Empty));
        services.AddHttpClient<ResendIdentityOtpEmailSender>();
        services.AddHttpClient<ResendPasswordResetEmailSender>();
        services.TryAddScoped<IPasswordResetEmailSender>(sp =>
            sp.GetRequiredService<ResendPasswordResetEmailSender>());
        services.TryAddScoped<IIdentityOtpEmailSender>(sp =>
            sp.GetRequiredService<ResendIdentityOtpEmailSender>());

        services.TryAddScoped<
            IAuthenticatedSessionReader,
            SqlAuthenticatedSessionReader>();

        services.TryAddScoped<
            IIdentitySessionRevocationStore,
            SqlIdentitySessionRevocationStore>();

        services.TryAddScoped<SessionLogoutService>();

        return services;
    }
}
