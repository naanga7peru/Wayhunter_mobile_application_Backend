
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class AiConfigurationStartupTests
{
    private static WebApplicationFactory<Program> CreateFactory(
        Dictionary<string, string?> settings)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(
                    (_, configuration) =>
                    {
                        configuration.AddInMemoryCollection(settings);
                    });
            });
    }

    [Fact]
    public void ValidAiConfiguration_ShouldStartSuccessfully()
    {
        using var factory = CreateFactory(new()
        {
            ["TravelPlanning:AiTrip:Enabled"] = "true",
            ["TravelPlanning:AiTrip:ApiKey"] = "dummy-test-key",
            ["TravelPlanning:AiTrip:Model"] = "test-model",
            ["TravelPlanning:AiTrip:TimeoutSeconds"] = "30",
            ["TravelPlanning:AiTrip:MaxRequestsPerUserPerDay"] = "10"
        });

        using var client = factory.CreateClient();

        var options = factory.Services
            .GetRequiredService<IOptions<AiTripProviderOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.Equal("test-model", options.Model);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.Equal(10, options.MaxRequestsPerUserPerDay);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void MissingApiKey_ShouldRejectStartup()
    {
        using var factory = CreateFactory(new()
        {
            ["TravelPlanning:AiTrip:Enabled"] = "true",
            ["TravelPlanning:AiTrip:ApiKey"] = "",
            ["TravelPlanning:AiTrip:Model"] = "test-model"
        });

        var exception =
            Assert.Throws<OptionsValidationException>(() =>
            {
                using var client = factory.CreateClient();
            });

        Assert.Contains(
            "AI provider configuration is invalid",
            exception.Message);
    }

    [Fact]
    public void InvalidTimeout_ShouldRejectStartup()
    {
        using var factory = CreateFactory(new()
        {
            ["TravelPlanning:AiTrip:Enabled"] = "true",
            ["TravelPlanning:AiTrip:ApiKey"] = "dummy-test-key",
            ["TravelPlanning:AiTrip:Model"] = "test-model",
            ["TravelPlanning:AiTrip:TimeoutSeconds"] = "2"
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            using var client = factory.CreateClient();
        });
    }
}
