namespace Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;

/// <summary>
/// Provisional M7 persistence model.
/// Not registered in the shared DbContext pending DB approval.
/// </summary>
public sealed class AdvertisementImageUploadOperationRow
{
    public Guid UploadOperationId { get; set; }
    public Guid AdvertisementId { get; set; }

    public string AdministratorId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;

    public string PreviousImageUrl { get; set; } = string.Empty;
    public string? ReplacementImageUrl { get; set; }
    public string? ProviderAssetId { get; set; }

    public int State { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? NextRetryAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastErrorCode { get; set; }

    public Guid? ClaimToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public long Revision { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public string? CleanupDecisionCode { get; set; }
    public DateTimeOffset? CleanupDecisionAtUtc { get; set; }
}