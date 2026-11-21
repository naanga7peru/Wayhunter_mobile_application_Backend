using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageRecoveryFencingPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private static AdvertisementImageUploadOperation Current() => new()
    {
        UploadOperationId = Guid.NewGuid(),
        AdvertisementId = Guid.NewGuid(),
        AdministratorId = "trusted-admin",
        PreviousImageUrl = "https://example.com/old.png",
        State = AdvertisementImageRecoveryState.SqlOutcomeUnknown,
        CreatedAtUtc = Now.AddMinutes(-5),
        UpdatedAtUtc = Now.AddMinutes(-1),
        ClaimToken = Guid.NewGuid(),
        LeaseExpiresAtUtc = Now.AddMinutes(1),
        Revision = 4
    };

    private static bool Save(
        AdvertisementImageUploadOperation current,
        AdvertisementImageUploadOperation proposed,
        Guid? token = null,
        long? revision = null) =>
        AdvertisementImageRecoveryFencingPolicy.CanSave(
            current,
            proposed,
            revision ?? current.Revision,
            token ?? current.ClaimToken!.Value,
            Now);

    [Fact]
    public void ValidClaimAndNextRevision_AcceptsVerificationRetry()
    {
        var current = Current();
        var proposed = current with
        {
            Revision = 5,
            RetryCount = 1,
            UpdatedAtUtc = Now,
            NextRetryAtUtc = Now.AddMinutes(2)
        };

        Assert.True(Save(current, proposed));
    }

    [Fact]
    public void WrongClaimToken_IsRejected()
    {
        var current = Current();
        Assert.False(Save(
            current,
            current with { Revision = 5 },
            token: Guid.NewGuid()));
    }

    [Fact]
    public void ExpiredLease_IsRejected()
    {
        var current = Current() with
        {
            LeaseExpiresAtUtc = Now
        };

        Assert.False(Save(
            current,
            current with { Revision = 5 }));
    }

    [Fact]
    public void StaleRevision_IsRejected()
    {
        var current = Current();

        Assert.False(Save(
            current,
            current with { Revision = 5 },
            revision: 3));
    }

    [Fact]
    public void RevisionMustAdvanceExactlyOnce()
    {
        var current = Current();

        Assert.False(Save(
            current,
            current with { Revision = 6 }));
    }

    [Fact]
    public void OperationIdentity_CannotBeReassigned()
    {
        var current = Current();

        Assert.False(Save(
            current,
            current with
            {
                Revision = 5,
                AdvertisementId = Guid.NewGuid()
            }));
    }

    [Fact]
    public void UploadedAssetIdentity_CannotChange()
    {
        var current = Current() with
        {
            ProviderAssetId = "wayhunter/advertisements/owned-asset"
        };

        Assert.False(Save(
            current,
            current with
            {
                Revision = 5,
                ProviderAssetId = "another-asset"
            }));
    }

    [Fact]
    public void GenericSave_CannotAuthorizeCleanup()
    {
        var current = Current() with
        {
            State = AdvertisementImageRecoveryState.Conflict
        };

        Assert.False(Save(
            current,
            current with
            {
                Revision = 5,
                State = AdvertisementImageRecoveryState.CleanupPending
            }));
    }
}