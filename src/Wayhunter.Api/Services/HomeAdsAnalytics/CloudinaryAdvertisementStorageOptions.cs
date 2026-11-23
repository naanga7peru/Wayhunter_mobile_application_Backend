namespace Wayhunter.Api.Services.HomeAdsAnalytics;

public sealed class CloudinaryAdvertisementStorageOptions
{
    public const string SectionName = "Advertisements:Cloudinary";

    public bool Enabled { get; set; }
    public string? CloudName { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }

    public bool IsValid => !Enabled ||
        (System.Text.RegularExpressions.Regex.IsMatch(
             CloudName ?? string.Empty,
             @"\A[a-zA-Z0-9_-]+\z",
             System.Text.RegularExpressions.RegexOptions.CultureInvariant) &&
         !string.IsNullOrWhiteSpace(ApiKey) &&
         !string.IsNullOrWhiteSpace(ApiSecret));
}
