
using System;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

public static class AdvertisementUrlValidator
{
    private const int MaxUrlLength = 2048;

    // Image URL is mandatory.
    public static bool IsValidImageUrl(string? imageUrl)
    {
        return IsSafeHttpsUrl(imageUrl);
    }

    // Destination URL is optional.
    public static bool IsValidDestinationUrl(string? destinationUrl)
    {
        if (destinationUrl is null)
            return true;

        return IsSafeHttpsUrl(destinationUrl);
    }

    private static bool IsSafeHttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Length > MaxUrlLength)
            return false;

        if (value != value.Trim())
            return false;

        // Reject control characters and backslashes.
        foreach (char character in value)
        {
            if (char.IsControl(character) || character == '\\')
                return false;
        }

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        // HTTPS only.
        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;

        // Reject URLs containing usernames or passwords.
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return false;

        // Reject loopback addresses.
        if (uri.IsLoopback)
            return false;

        // Restrict to standard HTTPS port.
        if (!uri.IsDefaultPort)
            return false;

        var host = uri.IdnHost;

        // Require a DNS hostname, not an IP address.
        if (Uri.CheckHostName(host) != UriHostNameType.Dns)
            return false;

        if (!host.Contains('.'))
            return false;

        if (host.EndsWith(".", StringComparison.Ordinal))
            return false;

        if (host.EndsWith(
                ".localhost",
                StringComparison.OrdinalIgnoreCase))
            return false;

        if (host.EndsWith(
                ".local",
                StringComparison.OrdinalIgnoreCase))
            return false;

        if (host.EndsWith(
                ".internal",
                StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
