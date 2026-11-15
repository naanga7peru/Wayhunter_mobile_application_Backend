
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Extensions;
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningAiConfigurationTests
{
    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();

        services.AddTravelPlanningAiConfiguration(
            configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void DisabledProvider_WithoutApiKey_ShouldPass()
    {
        using var provider = BuildProvider(new());

        var options = provider
            .GetRequiredService<IOptions<AiTripProviderOptions>>()
            .Value;

        Assert.False(options.Enabled);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void EnabledProvider_WithValidConfiguration_ShouldPass()
    {
        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:AiTrip:Enabled"] = "true",
            ["TravelPlanning:AiTrip:ApiKey"] = "dummy-test-key",
            ["TravelPlanning:AiTrip:Model"] = "test-model",
            ["TravelPlanning:AiTrip:TimeoutSeconds"] = "30",
            ["TravelPlanning:AiTrip:MaxRequestsPerUserPerDay"] = "10"
        });

        var options = provider
            .GetRequiredService<IOptions<AiTripProviderOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.Equal("test-model", options.Model);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.Equal(10, options.MaxRequestsPerUserPerDay);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void EnabledProvider_WithoutApiKey_ShouldFail()
    {
        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:AiTrip:Enabled"] = "true",
            ["TravelPlanning:AiTrip:Model"] = "test-model"
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider
                .GetRequiredService<IOptions<AiTripProviderOptions>>()
                .Value;
        });
    }

    [Fact]
    public void EnabledProvider_WithoutModel_ShouldFail()
    {
        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:AiTrip:Enabled"] = "true",
            ["TravelPlanning:AiTrip:ApiKey"] = "dummy-test-key"
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider
                .GetRequiredService<IOptions<AiTripProviderOptions>>()
                .Value;
        });
    }

    [Fact]
    public void InvalidTimeout_ShouldFail()
    {
        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:AiTrip:TimeoutSeconds"] = "2"
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider
                .GetRequiredService<IOptions<AiTripProviderOptions>>()
                .Value;
        });
    }

    [Fact]
    public void InvalidDailyLimit_ShouldFail()
    {
        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:AiTrip:MaxRequestsPerUserPerDay"] = "0"
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider
                .GetRequiredService<IOptions<AiTripProviderOptions>>()
                .Value;
        });
    }

    [Fact]
    public void ValidationError_ShouldNotExposeApiKey()
    {
        const string secret = "dummy-private-test-secret";

        using var provider = BuildProvider(new()
        {
            ["TravelPlanning:AiTrip:Enabled"] = "true",
            ["TravelPlanning:AiTrip:ApiKey"] = secret,
            ["TravelPlanning:AiTrip:Model"] = ""
        });

        var exception =
            Assert.Throws<OptionsValidationException>(() =>
            {
                _ = provider
                    .GetRequiredService<
                        IOptions<AiTripProviderOptions>>()
                    .Value;
            });

        Assert.DoesNotContain(
            secret,
            exception.Message,
            StringComparison.Ordinal);
    }
}
