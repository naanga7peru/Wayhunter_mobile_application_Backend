
namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

public static class CoinLedgerVersionPolicy
{
    public static string RequireRuleVersion(
        string? ruleVersion)
    {
        if (string.IsNullOrWhiteSpace(ruleVersion))
            throw new ArgumentException(
                "Rule version is required.",
                nameof(ruleVersion));

        return ruleVersion.Trim();
    }

    public static bool MatchesRuleVersion(
        string storedVersion,
        string suppliedVersion)
    {
        string stored =
            RequireRuleVersion(storedVersion);

        string supplied =
            RequireRuleVersion(suppliedVersion);

        return string.Equals(
            stored,
            supplied,
            StringComparison.Ordinal);
    }

    // Proposed optimistic concurrency check.
    // Actual atomic database enforcement is pending.
    public static bool MatchesWalletVersion(
        long currentVersion,
        long expectedVersion)
    {
        if (currentVersion < 0)
            throw new ArgumentOutOfRangeException(
                nameof(currentVersion));

        if (expectedVersion < 0)
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion));

        return currentVersion == expectedVersion;
    }
}
