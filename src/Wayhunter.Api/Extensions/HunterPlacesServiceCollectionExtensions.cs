
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;

namespace Wayhunter.Api.Extensions;

public static class HunterPlacesServiceCollectionExtensions
{
    // M4 - Trusted M1 Identity Integration
    public static IServiceCollection AddHunterPlacesTrustedIdentity(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Reuse M1's trusted server-side account reader.
        services.TryAddScoped<
            ITrustedIdentityAccountReader,
            AuthenticatedSessionAccountReaderAdapter>();

        // Resolve M4 HunterId from authenticated M1 account.
        services.TryAddScoped<
            IHunterCaptureSessionIdentityResolver,
            TrustedHunterCaptureSessionIdentityResolver>();

        return services;
    }

    // M4 - Capture Session Application Services
    public static IServiceCollection AddHunterPlacesApplication(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Validate Hunter submission ownership and Draft status.
        services.TryAddScoped<
            HunterCaptureSubmissionOwnershipService>();

        // Resolve authenticated Hunter identity.
        services.TryAddScoped<
            HunterCaptureSessionIdentityResolutionService>();

        // Create and consume capture sessions.
        services.TryAddScoped<
            HunterCaptureSessionService>();

        // Basic capture-session handler.
        services.TryAddScoped<
            HunterStartCaptureSessionHandler>();

        // Authorized capture-session handler.
        services.TryAddScoped<
            HunterStartAuthorizedCaptureSessionHandler>();

        services.TryAddScoped<
            HunterConsumeAuthorizedCaptureSessionHandler>();

        return services;
    }
}
