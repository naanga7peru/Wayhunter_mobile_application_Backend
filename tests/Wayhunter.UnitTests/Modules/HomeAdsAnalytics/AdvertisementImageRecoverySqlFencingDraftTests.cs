using System.Text.RegularExpressions;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageRecoverySqlFencingDraftTests
{
    [Fact]
    public void Claim_UsesAtomicConditionalUpdate()
    {
        var sql =
            AdvertisementImageRecoverySqlFencingDraft.ClaimUnknownOutcome;

        Assert.Single(
            Regex.Matches(sql, @"\bUPDATE\b").Cast<Match>());

        Assert.Contains(
            "UPDATE [dbo].[AdvertisementImageUploadOperations]",
            sql);

        Assert.Contains(
            "[UploadOperationId] = @UploadOperationId",
            sql);

        Assert.Contains(
            "[State] IN (1, 3)",
            sql);

        Assert.Contains(
            "OUTPUT",
            sql);

        Assert.Contains(
            "INSERTED.[Revision]",
            sql);
    }

    [Fact]
    public void Claim_UsesSqlClockAndEnforcesRetryDueTime()
    {
        var sql =
            AdvertisementImageRecoverySqlFencingDraft.ClaimUnknownOutcome;

        Assert.Contains("SYSUTCDATETIME()", sql);
        Assert.Contains("DATEADD(SECOND, @LeaseSeconds", sql);
        Assert.Contains("[NextRetryAtUtc] <=", sql);
        Assert.Contains("[LeaseExpiresAtUtc] <=", sql);
        Assert.Contains("[ClaimToken] IS NULL", sql);
    }

    [Fact]
    public void Save_EnforcesLeaseTokenAndRevision()
    {
        var sql =
            AdvertisementImageRecoverySqlFencingDraft.SaveVerificationOutcome;

        Assert.Single(
            Regex.Matches(sql, @"\bUPDATE\b").Cast<Match>());

        Assert.Contains("[ClaimToken] = @ClaimToken", sql);
        Assert.Contains("[LeaseExpiresAtUtc] >", sql);
        Assert.Contains("[Revision] = @ExpectedRevision", sql);
        Assert.Contains("SYSUTCDATETIME()", sql);
        Assert.Contains(
            "[Revision] = [Revision] + 1",
            sql);
    }

    [Fact]
    public void Save_OnlyAllowsVerificationRetryOrManualReview()
    {
        var sql =
            AdvertisementImageRecoverySqlFencingDraft.SaveVerificationOutcome;

        Assert.Contains("[State] IN (1, 3)", sql);
        Assert.Contains("@NextState = [State]", sql);
        Assert.Contains("@NextState = 8", sql);
        Assert.Contains("@NextRetryCount >= [RetryCount]", sql);
        Assert.Contains("@NextRetryAtUtc IS NULL", sql);

        Assert.False(
            sql.Contains(
                "[CleanupDecisionCode] =",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Claim_RejectsEmptyTokenAndUnboundedLease()
    {
        var sql =
            AdvertisementImageRecoverySqlFencingDraft.ClaimUnknownOutcome;

        Assert.Contains(
            "AND @ClaimToken <> '00000000-0000-0000-0000-000000000000'",
            sql);

        Assert.Contains(
            "AND @LeaseSeconds BETWEEN 30 AND 300",
            sql);
    }
    [Fact]
    public void Commands_DoNotContainDeleteOrDynamicSql()
    {
        foreach (var sql in new[]
        {
            AdvertisementImageRecoverySqlFencingDraft.ClaimUnknownOutcome,
            AdvertisementImageRecoverySqlFencingDraft.SaveVerificationOutcome
        })
        {
            Assert.False(
                Regex.IsMatch(sql, @"\bDELETE\b",
                    RegexOptions.IgnoreCase));

            Assert.False(
                Regex.IsMatch(sql, @"\bEXEC\b",
                    RegexOptions.IgnoreCase));

            Assert.False(
                sql.Contains(
                    "sp_executesql",
                    StringComparison.OrdinalIgnoreCase));

            Assert.Contains("@UploadOperationId", sql);
        }
    }
}