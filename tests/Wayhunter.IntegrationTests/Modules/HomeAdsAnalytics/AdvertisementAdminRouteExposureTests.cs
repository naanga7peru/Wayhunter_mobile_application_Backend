
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementAdminRouteExposureTests
{
    private const string TestAdvertisementId =
        "be153a1e-f789-4d9f-93a9-e907ca1d68ab";

    // M7 - Admin Advertisement Runtime Authorization

    [Theory]
    [InlineData(AdvertisementAdminOperation.Create)]
    [InlineData(AdvertisementAdminOperation.Update)]
    [InlineData(AdvertisementAdminOperation.ChangeStatus)]
    public async Task AdminWriteRoute_RequiresAuthentication(
        AdvertisementAdminOperation operation)
    {
        var definition =
            AdvertisementAdminEndpointContract.GetDefinition(operation);

        Assert.Equal(
            IdentityEndpointAccess.Administrator,
            definition.RequiredAccess);

        var route = definition.Route.Replace(
            "{advertisementId:guid}",
            TestAdvertisementId,
            StringComparison.Ordinal);

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureLogging(logging => logging.ClearProviders()));
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            new HttpMethod(definition.HttpMethod),
            "/" + route);

        using var response = await client.SendAsync(request);

        // Admin CRUD routes exist, but anonymous access is denied.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AdvertisementImageUpload_RequiresAuthentication()
    {
        var definition =
            AdvertisementAdminEndpointContract.GetDefinition(
                AdvertisementAdminOperation.UploadImage);

        Assert.Equal(
            IdentityEndpointAccess.Administrator,
            definition.RequiredAccess);

        var route = definition.Route.Replace(
            "{advertisementId:guid}",
            TestAdvertisementId,
            StringComparison.Ordinal);

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureLogging(logging => logging.ClearProviders()));
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            new HttpMethod(definition.HttpMethod),
            "/" + route);

        using var response = await client.SendAsync(request);

        // Upload route exists, but anonymous access is denied.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
