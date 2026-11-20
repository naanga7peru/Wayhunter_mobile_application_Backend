using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageRecoveryPolicyTests
{
    private static AdvertisementImageUploadOperation Operation(
        AdvertisementImageRecoveryState state,
        Guid token,
        DateTimeOffset now,
        int retries = 0) => new()
    {
        UploadOperationId = Guid.NewGuid(),
        AdvertisementId = Guid.NewGuid(),
        AdministratorId = "trusted-admin",
        PreviousImageUrl = "https://example.com/old.png",
        State = state,
        CreatedAtUtc = now.AddMinutes(-5),
        UpdatedAtUtc = now,
        RetryCount = retries,
        ClaimToken = token,
        LeaseExpiresAtUtc = now.AddMinutes(1)
    };

    [Fact]
    public void UncertainSqlOutcome_CannotDirectlyEnterCleanup()
    {
        Assert.False(AdvertisementImageRecoveryPolicy.CanTransition(
            AdvertisementImageRecoveryState.SqlOutcomeUnknown,
            AdvertisementImageRecoveryState.CleanupPending));

        Assert.True(AdvertisementImageRecoveryPolicy.CanTransition(
            AdvertisementImageRecoveryState.SqlOutcomeUnknown,
            AdvertisementImageRecoveryState.ManualReview));
    }

    [Fact]
    public void TerminalStates_CannotBeAutomaticallyReopened()
    {
        foreach (var state in new[]
        {
            AdvertisementImageRecoveryState.Committed,
            AdvertisementImageRecoveryState.CleanupConfirmed,
            AdvertisementImageRecoveryState.ManualReview
        })
        {
            Assert.False(AdvertisementImageRecoveryPolicy.CanTransition(
                state,
                AdvertisementImageRecoveryState.CleanupPending));
        }
    }

    [Fact]
    public void VerificationRetry_RequiresValidUnexpiredClaim()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid();
        var operation = Operation(
            AdvertisementImageRecoveryState.SqlOutcomeUnknown,
            token,
            now);

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageRecoveryPolicy.PlanVerificationRetry(
                operation,
                Guid.NewGuid(),
                now,
                3,
                TimeSpan.FromMinutes(2)));

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageRecoveryPolicy.PlanVerificationRetry(
                operation with
                {
                    LeaseExpiresAtUtc = now
                },
                token,
                now,
                3,
                TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void VerificationRetry_PreservesUncertainState()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid();
        var operation = Operation(
            AdvertisementImageRecoveryState.SqlOutcomeUnknown,
            token,
            now);

        var planned =
            AdvertisementImageRecoveryPolicy.PlanVerificationRetry(
                operation,
                token,
                now,
                3,
                TimeSpan.FromMinutes(2));

        Assert.Equal(
            AdvertisementImageRecoveryState.SqlOutcomeUnknown,
            planned.State);
        Assert.Equal(1, planned.RetryCount);
        Assert.Equal(now.AddMinutes(2), planned.NextRetryAtUtc);
    }

    [Fact]
    public void RetryExhaustion_EscalatesToManualReview()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid();
        var operation = Operation(
            AdvertisementImageRecoveryState.UploadOutcomeUnknown,
            token,
            now,
            retries: 2);

        var planned =
            AdvertisementImageRecoveryPolicy.PlanVerificationRetry(
                operation,
                token,
                now,
                3,
                TimeSpan.FromMinutes(2));

        Assert.Equal(
            AdvertisementImageRecoveryState.ManualReview,
            planned.State);
        Assert.Equal(3, planned.RetryCount);
        Assert.Null(planned.NextRetryAtUtc);
        Assert.Equal(
            "M7_RECOVERY_RETRY_EXHAUSTED",
            planned.LastErrorCode);
    }

    [Fact]
    public void CommittedOperation_CannotScheduleVerificationRetry()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid();
        var operation = Operation(
            AdvertisementImageRecoveryState.Committed,
            token,
            now);

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageRecoveryPolicy.PlanVerificationRetry(
                operation,
                token,
                now,
                3,
                TimeSpan.FromMinutes(2)));
    }
}