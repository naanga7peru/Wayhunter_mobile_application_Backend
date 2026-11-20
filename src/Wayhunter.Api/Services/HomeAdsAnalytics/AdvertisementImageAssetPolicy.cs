namespace Wayhunter.Api.Services.HomeAdsAnalytics;

/// <summary>
/// M7-owned advertisement image asset identity policy.
/// Asset paths are generated on the server, never by clients.
/// </summary>
public static class AdvertisementImageAssetPolicy
{
    private const string Prefix = "wayhunter/advertisements";

    public static string CreateAssetId(Guid advertisementId)
    {
        if (advertisementId == Guid.Empty)
            throw new ArgumentException(
                "Invalid advertisement ID.",
                nameof(advertisementId));

        return $"{Prefix}/{advertisementId:D}/{Guid.NewGuid():N}";
    }

    public static bool IsOwnedByAdvertisement(
        Guid advertisementId,
        string? assetId)
    {
        if (advertisementId == Guid.Empty ||
            string.IsNullOrWhiteSpace(assetId))
        {
            return false;
        }

        var expectedPrefix =
            $"{Prefix}/{advertisementId:D}/";

        if (!assetId.StartsWith(
                expectedPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = assetId[expectedPrefix.Length..];

        if (suffix.Length != 32)
            return false;

        if (!Guid.TryParseExact(suffix, "N", out var uniqueId))
            return false;

        return uniqueId != Guid.Empty;
    }
}