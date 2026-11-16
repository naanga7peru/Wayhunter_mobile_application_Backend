
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementMobileRouteSafetyTests
{
    // Pre-integration safety baseline.
    // The mobile advertisement endpoint is only a contract.
    // No HTTP route should be exposed until the approved
    // authentication, persistence and publication integration.

    [Fact]
    public async Task MobileAdvertisementRoute_ShouldNotBeExposed()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        using var response = await client.GetAsync("/" + route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("HomeBanner")]
    [InlineData("SponsoredPoi")]
    public async Task PlacementFilter_ShouldNotExposeRoute(
        string placement)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        var url = "/" + route + "?placement=" +
                  Uri.EscapeDataString(placement);

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DistrictFilter_ShouldNotExposeRoute()
    {
        Assert.False(
            AdvertisementMobileReadEndpointContract.DistrictTargetingReady);

        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        using var response = await client.GetAsync(
            "/" + route + "?district=Jaffna");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClientSuppliedTime_ShouldNotExposeRoute()
    {
        Assert.False(
            AdvertisementMobileReadEndpointContract
                .ClientSuppliedTimeSupported);

        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        using var response = await client.GetAsync(
            "/" + route + "?atUtc=2026-11-16T10%3A00%3A00Z");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task MobileAdvertisementWriteMethods_ShouldNotBeExposed(
        string method)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            "/" + route);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
