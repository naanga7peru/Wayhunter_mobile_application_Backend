
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class MapsConfigurationStartupTests
{
    // TEST 01:
    // Backend should start when Maps configuration is valid.

    [Fact]
    public void ApiStartup_ShouldLoadMapsConfiguration()
    {
        // Dummy values only. No real API keys.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(
                    (_, configuration) =>
                    {
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["TravelPlanning:Maps:EnablePlaces"]
                                    = "true",

                                ["TravelPlanning:Maps:PlacesServerApiKey"]
                                    = "dummy-test-only-key",

                                ["TravelPlanning:Maps:EnableRoutes"]
                                    = "false",

                                ["TravelPlanning:Maps:EnableGeocoding"]
                                    = "false"
                            });
                    });
            });

        // Start the ASP.NET Core test server.
        using var client = factory.CreateClient();

        // Read registered Maps options.
        var options = factory.Services
            .GetRequiredService<IOptions<MapsProviderOptions>>()
            .Value;

        Assert.True(options.EnablePlaces);
        Assert.False(options.EnableRoutes);
        Assert.False(options.EnableGeocoding);

        Assert.Equal(
            "dummy-test-only-key",
            options.PlacesServerApiKey);

        Assert.Empty(options.Validate());
    }

    // TEST 02:
    // Backend startup must fail when an enabled
    // Maps provider has no server API key.

    [Fact]
    public void ApiStartup_MissingRequiredMapsKey_ShouldFail()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(
                    (_, configuration) =>
                    {
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["TravelPlanning:Maps:EnablePlaces"]
                                    = "true",

                                ["TravelPlanning:Maps:PlacesServerApiKey"]
                                    = "",

                                ["TravelPlanning:Maps:EnableRoutes"]
                                    = "false",

                                ["TravelPlanning:Maps:EnableGeocoding"]
                                    = "false"
                            });
                    });
            });

        // ValidateOnStart should reject this configuration.
        var exception = Assert.Throws<OptionsValidationException>(
            () =>
            {
                using var client = factory.CreateClient();
            });

        Assert.Contains(
            "required server configuration",
            exception.Message);
    }
}
