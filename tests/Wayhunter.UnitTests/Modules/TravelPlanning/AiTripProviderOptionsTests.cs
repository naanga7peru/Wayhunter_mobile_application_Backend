
using System;
using System.Linq;
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class AiTripProviderOptionsTests
{
    private static AiTripProviderOptions ValidOptions() => new()
    {
        Enabled = true,
        ApiKey = "dummy-test-key",
        Model = "test-model",
        TimeoutSeconds = 30,
        MaxRequestsPerUserPerDay = 10
    };

    [Fact]
    public void DisabledProvider_WithoutKey_ShouldPass()
    {
        var options = new AiTripProviderOptions();

        Assert.False(options.Enabled);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void EnabledProvider_WithValidConfiguration_ShouldPass()
    {
        var options = ValidOptions();

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void EnabledProvider_WithoutApiKey_ShouldFail()
    {
        var options = ValidOptions();
        options.ApiKey = null;

        var errors = options.Validate();

        Assert.Contains(errors, error =>
            error.Contains("API key"));
    }

    [Fact]
    public void EnabledProvider_WithWhitespaceKey_ShouldFail()
    {
        var options = ValidOptions();
        options.ApiKey = "   ";

        Assert.NotEmpty(options.Validate());
    }

    [Fact]
    public void EnabledProvider_WithoutModel_ShouldFail()
    {
        var options = ValidOptions();
        options.Model = null;

        var errors = options.Validate();

        Assert.Contains(errors, error =>
            error.Contains("model"));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public void InvalidTimeout_ShouldFail(int timeout)
    {
        var options = ValidOptions();
        options.TimeoutSeconds = timeout;

        Assert.NotEmpty(options.Validate());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(120)]
    public void ValidTimeoutBoundaries_ShouldPass(int timeout)
    {
        var options = ValidOptions();
        options.TimeoutSeconds = timeout;

        Assert.Empty(options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidDailyLimit_ShouldFail(int limit)
    {
        var options = ValidOptions();
        options.MaxRequestsPerUserPerDay = limit;

        Assert.NotEmpty(options.Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void ValidDailyLimitBoundaries_ShouldPass(int limit)
    {
        var options = ValidOptions();
        options.MaxRequestsPerUserPerDay = limit;

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void ValidationErrors_ShouldNotExposeApiKey()
    {
        const string secret = "dummy-private-test-secret";

        var options = ValidOptions();
        options.ApiKey = secret;
        options.Model = "";
        options.TimeoutSeconds = 1;

        var errors = options.Validate();

        Assert.NotEmpty(errors);

        Assert.DoesNotContain(errors, error =>
            error.Contains(
                secret,
                StringComparison.Ordinal));
    }
}
