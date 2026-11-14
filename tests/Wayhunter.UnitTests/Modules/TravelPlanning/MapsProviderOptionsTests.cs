
using System;
using System.Linq;
using Xunit;
using Wayhunter.Infrastructure.Integrations.Maps;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class MapsProviderOptionsTests
{
    [Fact]
    public void DisabledProviders_ShouldNotRequireKeys()
    {
        var options = new MapsProviderOptions();

        var errors = options.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void EnabledGeocoding_WithoutKey_ShouldFail()
    {
        var options = new MapsProviderOptions
        {
            EnableGeocoding = true
        };

        var errors = options.Validate();

        Assert.Contains(
            "Geocoding server API key is required.",
            errors);
    }

    [Fact]
    public void EnabledPlaces_WithoutKey_ShouldFail()
    {
        var options = new MapsProviderOptions
        {
            EnablePlaces = true
        };

        var errors = options.Validate();

        Assert.Contains(
            "Places server API key is required.",
            errors);
    }

    [Fact]
    public void EnabledRoutes_WithoutKey_ShouldFail()
    {
        var options = new MapsProviderOptions
        {
            EnableRoutes = true
        };

        var errors = options.Validate();

        Assert.Contains(
            "Routes server API key is required.",
            errors);
    }

    [Fact]
    public void AllEnabled_WithoutKeys_ShouldReturnThreeErrors()
    {
        var options = new MapsProviderOptions
        {
            EnableGeocoding = true,
            EnablePlaces = true,
            EnableRoutes = true
        };

        var errors = options.Validate();

        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void WhitespaceApiKey_ShouldBeRejected()
    {
        var options = new MapsProviderOptions
        {
            EnablePlaces = true,
            PlacesServerApiKey = "   "
        };

        var errors = options.Validate();

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ConfiguredProviders_ShouldPass()
    {
        var options = new MapsProviderOptions
        {
            EnableGeocoding = true,
            EnablePlaces = true,
            EnableRoutes = true,

            GeocodingServerApiKey = "test-key-geo",
            PlacesServerApiKey = "test-key-places",
            RoutesServerApiKey = "test-key-routes"
        };

        var errors = options.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void DisabledProvider_WithMissingKey_ShouldPass()
    {
        var options = new MapsProviderOptions
        {
            EnableGeocoding = false,
            GeocodingServerApiKey = null
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void ValidationErrors_ShouldNotExposeApiKeys()
    {
        const string fakeSecret = "dummy-sensitive-test-key";

        var options = new MapsProviderOptions
        {
            EnablePlaces = true,
            PlacesServerApiKey = fakeSecret,
            EnableRoutes = true,
            RoutesServerApiKey = null
        };

        var errors = options.Validate();

        Assert.Single(errors);

        Assert.DoesNotContain(
            fakeSecret,
            string.Join(" ", errors));
    }
}
