
using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Weather;

namespace Wayhunter.Api.Extensions;

public static class TravelPlanningWeatherExtensions
{
    public static IServiceCollection AddTravelPlanningWeather(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<WeatherProviderOptions>()
            .Configure<IConfiguration>(
                (options, configuration) =>
                {
                    configuration
                        .GetSection(
                            WeatherProviderOptions.SectionName)
                        .Bind(options);
                })
            .Validate(
                options => options.Validate().Count == 0,
                "Weather provider configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton<WeatherHttpClientLifetime>();

        services.AddSingleton<OpenWeatherMapProvider>(provider =>
        {
            var client = provider
                .GetRequiredService<WeatherHttpClientLifetime>()
                .Client;

            var options = provider
                .GetRequiredService<IOptions<WeatherProviderOptions>>()
                .Value;

            return new OpenWeatherMapProvider(client, options);
        });

        services.AddSingleton<IWeatherProvider>(sp => sp.GetRequiredService<OpenWeatherMapProvider>());
        services.AddSingleton<IWeatherForecastProvider>(sp => sp.GetRequiredService<OpenWeatherMapProvider>());
        services.AddSingleton(sp => new WeatherForecastService(
            sp.GetRequiredService<IWeatherForecastProvider>(),
            sp.GetRequiredService<IOptions<WeatherProviderOptions>>().Value.GetCacheMaximumAge(),
            TimeProvider.System));

        services.AddSingleton<WeatherCacheService>(provider =>
        {
            var weatherProvider = provider
                .GetRequiredService<IWeatherProvider>();

            var options = provider
                .GetRequiredService<IOptions<WeatherProviderOptions>>()
                .Value;

            return new WeatherCacheService(
                weatherProvider,
                options.GetCacheMaximumAge());
        });

        return services;
    }
}

internal sealed class WeatherHttpClientLifetime : IDisposable
{
    public HttpClient Client { get; }

    public WeatherHttpClientLifetime()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime =
                TimeSpan.FromMinutes(5)
        };

        Client = new HttpClient(
            handler,
            disposeHandler: true)
        {
            Timeout =
                System.Threading.Timeout.InfiniteTimeSpan
        };
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}
