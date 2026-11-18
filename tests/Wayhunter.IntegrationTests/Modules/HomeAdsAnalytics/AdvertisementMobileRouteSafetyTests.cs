
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
    [Fact]
    public async Task MobileAdvertisementRoute_RequiresAuthentication()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        using var response = await client.GetAsync("/" + route);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("HomeBanner")]
    [InlineData("SponsoredPoi")]
    public async Task PlacementFilter_RequiresAuthentication(
        string placement)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        var url = "/" + route + "?placement=" +
                  Uri.EscapeDataString(placement);

        using var response = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task DistrictTargeting_RemainsUnsupported()
    {
        Assert.False(
            AdvertisementMobileReadEndpointContract
                .DistrictTargetingReady);

        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var route =
            AdvertisementMobileReadEndpointContract.ActiveAdsRoute;

        using var response = await client.GetAsync(
            "/" + route + "?district=Jaffna");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ClientSuppliedTime_RemainsUnsupported()
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

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task MobileAdvertisementWriteMethods_Return405(
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

        Assert.Equal(
            HttpStatusCode.MethodNotAllowed,
            response.StatusCode);
    }
}
