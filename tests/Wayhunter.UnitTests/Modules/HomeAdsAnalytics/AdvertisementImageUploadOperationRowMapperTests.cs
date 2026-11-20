using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageUploadOperationRowMapperTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static AdvertisementImageUploadOperation Operation()
    {
        var operationId = Guid.NewGuid();
        var advertisementId = Guid.NewGuid();

        return new AdvertisementImageUploadOperation
        {
            UploadOperationId = operationId,
            AdvertisementId = advertisementId,
            AdministratorId = "trusted-admin",
            PreviousImageUrl = "https://example.com/old.png",
            ProviderAssetId =
                $"wayhunter/advertisements/{advertisementId:D}/{operationId:N}",
            State = AdvertisementImageRecoveryState.PendingUpload,
            CreatedAtUtc = Now.AddMinutes(-5),
            UpdatedAtUtc = Now,
            RetryCount = 0,
            Revision = 0
        };
    }

    [Fact]
    public void NewRow_PreservesOperationAndProvider()
    {
        var operation = Operation();

        var row =
            AdvertisementImageUploadOperationRowMapper.ToNewRow(operation);

        Assert.Equal(operation.UploadOperationId, row.UploadOperationId);
        Assert.Equal(operation.AdvertisementId, row.AdvertisementId);
        Assert.Equal("Cloudinary", row.Provider);
        Assert.Equal(operation.ProviderAssetId, row.ProviderAssetId);
        Assert.Equal(0, row.State);
        Assert.Equal(0, row.Revision);
    }

    [Fact]
    public void RoundTrip_PreservesRecoveryFields()
    {
        var token = Guid.NewGuid();

        var operation = Operation() with
        {
            ReplacementImageUrl = "https://example.com/new.png",
            State = AdvertisementImageRecoveryState.SqlOutcomeUnknown,
            RetryCount = 2,
            Revision = 5,
            ClaimToken = token,
            LeaseExpiresAtUtc = Now.AddMinutes(5),
            NextRetryAtUtc = Now.AddMinutes(10),
            LastErrorCode = "SQL_OUTCOME_UNKNOWN"
        };

        var row =
            AdvertisementImageUploadOperationRowMapper.ToNewRow(operation);

        var restored =
            AdvertisementImageUploadOperationRowMapper.ToOperation(row);

        Assert.Equal(operation, restored);
    }

    [Fact]
    public void UnknownProvider_IsRejected()
    {
        var row =
            AdvertisementImageUploadOperationRowMapper.ToNewRow(Operation());

        row.Provider = "OtherProvider";

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadOperationRowMapper.ToOperation(row));
    }

    [Fact]
    public void UnknownRecoveryState_IsRejected()
    {
        var row =
            AdvertisementImageUploadOperationRowMapper.ToNewRow(Operation());

        row.State = 999;

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadOperationRowMapper.ToOperation(row));
    }

    [Fact]
    public void ForeignAssetId_IsRejected()
    {
        var operation = Operation() with
        {
            ProviderAssetId =
                $"wayhunter/advertisements/{Guid.NewGuid():D}/{Guid.NewGuid():N}"
        };

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadOperationRowMapper.ToNewRow(operation));
    }

    [Fact]
    public void InvalidClaimLeasePair_IsRejected()
    {
        var operation = Operation() with
        {
            ClaimToken = Guid.NewGuid(),
            LeaseExpiresAtUtc = null
        };

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadOperationRowMapper.ToNewRow(operation));
    }
}