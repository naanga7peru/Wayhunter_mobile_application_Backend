
using System;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementAdminEndpointContractTests
{
    // Tests 1-4: Every advertisement write operation
    // must require Administrator access.
    [Theory]
    [InlineData(
        AdvertisementAdminOperation.Create,
        "POST",
        "api/admin/advertisements")]
    [InlineData(
        AdvertisementAdminOperation.Update,
        "PUT",
        "api/admin/advertisements/{advertisementId:guid}")]
    [InlineData(
        AdvertisementAdminOperation.ChangeStatus,
        "PATCH",
        "api/admin/advertisements/{advertisementId:guid}/status")]
    [InlineData(
        AdvertisementAdminOperation.UploadImage,
        "POST",
        "api/admin/advertisements/{advertisementId:guid}/image")]
    public void AdminOperation_ShouldRequireAdministratorAccess(
        AdvertisementAdminOperation operation,
        string expectedMethod,
        string expectedRoute)
    {
        var definition =
            AdvertisementAdminEndpointContract.GetDefinition(
                operation);

        Assert.Equal(
            IdentityEndpointAccess.Administrator,
            definition.RequiredAccess);

        Assert.Equal(
            expectedMethod,
            definition.HttpMethod);

        Assert.Equal(
            expectedRoute,
            definition.Route);
    }

    // Tests 5-6: Unsupported operations must be rejected.
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void UnsupportedOperation_ShouldBeRejected(
        int operation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AdvertisementAdminEndpointContract
                .GetDefinition(
                    (AdvertisementAdminOperation)operation));
    }
}
