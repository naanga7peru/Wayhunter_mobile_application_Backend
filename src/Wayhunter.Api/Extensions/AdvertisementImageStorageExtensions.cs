using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Services.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Api.Extensions;

public static class AdvertisementImageStorageExtensions
{
    public static IServiceCollection AddConfiguredAdvertisementImageStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<CloudinaryAdvertisementStorageOptions>()
            .Bind(configuration.GetSection(
                CloudinaryAdvertisementStorageOptions.SectionName))
            .Validate(options => options.IsValid,
                "Cloudinary advertisement storage requires server credentials when enabled.")
            .ValidateOnStart();

        services.AddHttpClient("wayhunter-advertisement-cloudinary", client =>
            client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

        services.AddScoped<IAdvertisementImageStorage>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<CloudinaryAdvertisementStorageOptions>>()
                .Value;

            if (!options.Enabled)
                return new UnavailableAdvertisementImageStorage();

            if (!options.IsValid)
                throw new InvalidOperationException(
                    "Cloudinary advertisement storage configuration is invalid.");

            return new CloudinaryAdvertisementImageStorage(
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient("wayhunter-advertisement-cloudinary"),
                options.CloudName!,
                options.ApiKey!,
                options.ApiSecret!);
        });

        return services;
    }
}
