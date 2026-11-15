
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementAdminRouteExposureTests
{
    private const string TestAdvertisementId =
        "be153a1e-f789-4d9f-93a9-e907ca1d68ab";

    // Pre-authentication baseline only.
    //
    // Until M1 JWT/RBAC is integrated, no Admin Ads
    // write route may be exposed in the running API.
    //
    // These tests expect 404 because the routes
    // do not currently exist.
    //
    // Once secure endpoints are implemented,
    // replace these tests with:
    // Anonymous -> 401 Unauthorized
    // Non-admin -> 403 Forbidden

    [Theory]
    [InlineData(AdvertisementAdminOperation.Create)]
    [InlineData(AdvertisementAdminOperation.Update)]
    [InlineData(AdvertisementAdminOperation.ChangeStatus)]
    [InlineData(AdvertisementAdminOperation.UploadImage)]
    public async Task AdminWriteRoute_ShouldRemainUnavailable(
        AdvertisementAdminOperation operation)
    {
        var definition =
            AdvertisementAdminEndpointContract.GetDefinition(
                operation);

        // All Admin write operations require Administrator.
        Assert.Equal(
            IdentityEndpointAccess.Administrator,
            definition.RequiredAccess);

        var route = definition.Route.Replace(
            "{advertisementId:guid}",
            TestAdvertisementId,
            StringComparison.Ordinal);

        using var factory =
            new WebApplicationFactory<Program>();

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            new HttpMethod(definition.HttpMethod),
            "/" + route);

        using var response =
            await client.SendAsync(request);

        // No Admin Ads write endpoints are registered yet.
        // This is not a JWT/RBAC enforcement assertion.
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
