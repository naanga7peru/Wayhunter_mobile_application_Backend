
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Extensions;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningMapsConfigurationTests
{
    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();

        services.AddTravelPlanningMapsConfiguration(
            configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void DisabledProviders_ShouldStartWithoutKeys()
    {
        using var provider = BuildProvider(new());

        var options = provider
            .GetRequiredService<IOptions<MapsProviderOptions>>()
            .Value;

        Assert.False(options.EnablePlaces);
        Assert.False(options.EnableRoutes);
        Assert.False(options.EnableGeocoding);
    }

    [Theory]
    [InlineData("EnablePlaces")]
    [InlineData("EnableRoutes")]
    [InlineData("EnableGeocoding")]
    public void EnabledProvider_WithoutKey_ShouldFail(
        string enabledSetting)
    {
        using var provider = BuildProvider(new()
        {
            [$"{MapsProviderOptions.SectionName}:{enabledSetting}"]
                = "true"
        });

        var exception =
            Assert.Throws<OptionsValidationException>(() =>
            {
                _ = provider
                    .GetRequiredService<
                        IOptions<MapsProviderOptions>>()
                    .Value;
            });

        Assert.Contains(
            "required server configuration",
            exception.Message);
    }

    [Fact]
    public void AllProviders_WithKeys_ShouldPass()
    {
        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:Maps:EnablePlaces"] = "true",
            ["TravelPlanning:Maps:EnableRoutes"] = "true",
            ["TravelPlanning:Maps:EnableGeocoding"] = "true",

            ["TravelPlanning:Maps:PlacesServerApiKey"]
                = "dummy-places-key",

            ["TravelPlanning:Maps:RoutesServerApiKey"]
                = "dummy-routes-key",

            ["TravelPlanning:Maps:GeocodingServerApiKey"]
                = "dummy-geocoding-key"
        });

        var options = provider
            .GetRequiredService<IOptions<MapsProviderOptions>>()
            .Value;

        Assert.True(options.EnablePlaces);
        Assert.True(options.EnableRoutes);
        Assert.True(options.EnableGeocoding);

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void WhitespaceKey_ShouldFailValidation()
    {
        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:Maps:EnablePlaces"] = "true",
            ["TravelPlanning:Maps:PlacesServerApiKey"] = "  "
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider
                .GetRequiredService<IOptions<MapsProviderOptions>>()
                .Value;
        });
    }

    [Fact]
    public void ValidationError_ShouldNotExposeSecret()
    {
        const string dummySecret =
            "dummy-private-test-value-123";

        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:Maps:EnablePlaces"] = "true",
            ["TravelPlanning:Maps:PlacesServerApiKey"]
                = dummySecret,

            ["TravelPlanning:Maps:EnableRoutes"] = "true"
        });

        var exception =
            Assert.Throws<OptionsValidationException>(() =>
            {
                _ = provider
                    .GetRequiredService<
                        IOptions<MapsProviderOptions>>()
                    .Value;
            });

        Assert.DoesNotContain(
            dummySecret,
            exception.Message);
    }
}
