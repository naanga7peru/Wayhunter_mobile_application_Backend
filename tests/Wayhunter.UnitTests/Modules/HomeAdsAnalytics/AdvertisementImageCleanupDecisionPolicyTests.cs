using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageCleanupDecisionPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static AdvertisementImageUploadOperation Operation(
        Guid token) => new()
    {
        UploadOperationId = Guid.NewGuid(),
        AdvertisementId = Guid.NewGuid(),
        AdministratorId = "trusted-admin",
        PreviousImageUrl = "https://example.com/old.png",
        ReplacementImageUrl = "https://example.com/new.png",
        ProviderAssetId = "wayhunter/advertisements/owned",
        State = AdvertisementImageRecoveryState.Conflict,
        CreatedAtUtc = Now.AddHours(-4),
        UpdatedAtUtc = Now.AddHours(-2),
        ClaimToken = token,
        LeaseExpiresAtUtc = Now.AddMinutes(1),
        Revision = 1
    };

    private static AdvertisementImageCleanupAssessment Assess(
        AdvertisementImageUploadOperation operation,
        AdvertisementAssetReferenceDecision decision,
        Guid token,
        bool owned = true) =>
        AdvertisementImageCleanupDecisionPolicy.Assess(
            operation,
            new AdvertisementAssetReferenceResult(decision, "TEST"),
            owned,
            token,
            Now,
            TimeSpan.FromHours(1));

    [Theory]
    [InlineData(AdvertisementAssetReferenceDecision.Unknown)]
    [InlineData(AdvertisementAssetReferenceDecision.Referenced)]
    [InlineData(AdvertisementAssetReferenceDecision.UnreferencedButUnsafe)]
    public void UnsafeReferenceResults_AlwaysPreserve(
        AdvertisementAssetReferenceDecision decision)
    {
        var token = Guid.NewGuid();

        Assert.Equal(
            AdvertisementImageCleanupAssessment.Preserve,
            Assess(Operation(token), decision, token));
    }

    [Fact]
    public void SafeReferenceResult_StillRequiresAtomicSqlReservation()
    {
        var token = Guid.NewGuid();

        Assert.Equal(
            AdvertisementImageCleanupAssessment.RequiresAtomicSqlReservation,
            Assess(
                Operation(token),
                AdvertisementAssetReferenceDecision.SafeForCleanup,
                token));
    }

    [Fact]
    public void UnverifiedOwnership_MustPreserve()
    {
        var token = Guid.NewGuid();

        Assert.Equal(
            AdvertisementImageCleanupAssessment.Preserve,
            Assess(
                Operation(token),
                AdvertisementAssetReferenceDecision.SafeForCleanup,
                token,
                owned: false));
    }

    [Fact]
    public void UnexpiredGracePeriod_MustPreserve()
    {
        var token = Guid.NewGuid();
        var operation = Operation(token) with
        {
            UpdatedAtUtc = Now.AddMinutes(-10)
        };

        Assert.Equal(
            AdvertisementImageCleanupAssessment.Preserve,
            Assess(
                operation,
                AdvertisementAssetReferenceDecision.SafeForCleanup,
                token));
    }

    [Fact]
    public void InvalidOrExpiredClaim_MustPreserve()
    {
        var token = Guid.NewGuid();
        var operation = Operation(token);

        Assert.Equal(
            AdvertisementImageCleanupAssessment.Preserve,
            Assess(
                operation,
                AdvertisementAssetReferenceDecision.SafeForCleanup,
                Guid.NewGuid()));

        Assert.Equal(
            AdvertisementImageCleanupAssessment.Preserve,
            Assess(
                operation with { LeaseExpiresAtUtc = Now },
                AdvertisementAssetReferenceDecision.SafeForCleanup,
                token));
    }

    [Fact]
    public void AmbiguousSqlOutcome_MustPreserve()
    {
        var token = Guid.NewGuid();

        Assert.Equal(
            AdvertisementImageCleanupAssessment.Preserve,
            Assess(
                Operation(token) with
                {
                    State = AdvertisementImageRecoveryState.SqlOutcomeUnknown
                },
                AdvertisementAssetReferenceDecision.SafeForCleanup,
                token));
    }

    [Fact]
    public void MissingProviderAssetId_MustPreserve()
    {
        var token = Guid.NewGuid();

        Assert.Equal(
            AdvertisementImageCleanupAssessment.Preserve,
            Assess(
                Operation(token) with { ProviderAssetId = null },
                AdvertisementAssetReferenceDecision.SafeForCleanup,
                token));
    }
}