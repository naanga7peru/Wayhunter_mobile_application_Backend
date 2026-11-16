
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Weather;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningWeatherRegistrationTests
{
    [Fact]
    public void Module_ShouldRegisterWeatherProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().Build());

        services.AddTravelPlanningModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

        var weatherProvider = provider
            .GetRequiredService<IWeatherProvider>();

        Assert.IsType<OpenWeatherMapProvider>(
            weatherProvider);
    }

    [Fact]
    public void WeatherCache_ShouldBeSharedAcrossScopes()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().Build());

        services.AddTravelPlanningModule();

        using var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var cache1 = scope1.ServiceProvider
            .GetRequiredService<WeatherCacheService>();

        var cache2 = scope2.ServiceProvider
            .GetRequiredService<WeatherCacheService>();

        Assert.Same(cache1, cache2);
    }

    [Fact]
    public void MissingApiKey_ShouldFailWhenEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TravelPlanning:Weather:Enabled"] = "true"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(config);
        services.AddTravelPlanningWeather();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider
                .GetRequiredService<IOptions<WeatherProviderOptions>>()
                .Value);
    }

    [Fact]
    public void ValidConfiguration_ShouldLoadOptions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TravelPlanning:Weather:Enabled"] = "true",
                    ["TravelPlanning:Weather:ApiKey"] = "test-key",
                    ["TravelPlanning:Weather:CacheMaxAgeMinutes"] = "20",
                    ["TravelPlanning:Weather:TimeoutSeconds"] = "10"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(config);
        services.AddTravelPlanningWeather();

        using var provider = services.BuildServiceProvider();

        var options = provider
            .GetRequiredService<IOptions<WeatherProviderOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.Equal(20, options.CacheMaxAgeMinutes);
        Assert.Equal(10, options.TimeoutSeconds);
        Assert.Empty(options.Validate());
    }
}
