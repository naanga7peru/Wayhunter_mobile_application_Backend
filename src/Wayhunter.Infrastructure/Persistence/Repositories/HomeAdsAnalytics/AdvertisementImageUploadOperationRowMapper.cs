using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;

/// <summary>
/// Provisional mapping for M7 recovery persistence.
/// No SQL connection, writes, migrations or provider calls.
/// </summary>
public static class AdvertisementImageUploadOperationRowMapper
{
    private const string ProviderName = "Cloudinary";

    public static AdvertisementImageUploadOperationRow ToNewRow(
        AdvertisementImageUploadOperation operation)
    {
        Validate(operation);

        return new AdvertisementImageUploadOperationRow
        {
            UploadOperationId = operation.UploadOperationId,
            AdvertisementId = operation.AdvertisementId,
            AdministratorId = operation.AdministratorId,
            Provider = ProviderName,
            PreviousImageUrl = operation.PreviousImageUrl,
            ReplacementImageUrl = operation.ReplacementImageUrl,
            ProviderAssetId = operation.ProviderAssetId,
            State = (int)operation.State,
            CreatedAtUtc = operation.CreatedAtUtc,
            UpdatedAtUtc = operation.UpdatedAtUtc,
            CompletedAtUtc = operation.CompletedAtUtc,
            NextRetryAtUtc = operation.NextRetryAtUtc,
            RetryCount = operation.RetryCount,
            LastErrorCode = operation.LastErrorCode,
            ClaimToken = operation.ClaimToken,
            LeaseExpiresAtUtc = operation.LeaseExpiresAtUtc,
            Revision = operation.Revision,
            CleanupDecisionCode = operation.CleanupDecisionCode,
            CleanupDecisionAtUtc = operation.CleanupDecisionAtUtc
        };
    }

    public static AdvertisementImageUploadOperation ToOperation(
        AdvertisementImageUploadOperationRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!string.Equals(
                row.Provider,
                ProviderName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Unexpected advertisement image provider.");
        }

        var operation = new AdvertisementImageUploadOperation
        {
            UploadOperationId = row.UploadOperationId,
            AdvertisementId = row.AdvertisementId,
            AdministratorId = row.AdministratorId,
            PreviousImageUrl = row.PreviousImageUrl,
            ReplacementImageUrl = row.ReplacementImageUrl,
            ProviderAssetId = row.ProviderAssetId,
            State = (AdvertisementImageRecoveryState)row.State,
            CreatedAtUtc = row.CreatedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            NextRetryAtUtc = row.NextRetryAtUtc,
            RetryCount = row.RetryCount,
            LastErrorCode = row.LastErrorCode,
            ClaimToken = row.ClaimToken,
            LeaseExpiresAtUtc = row.LeaseExpiresAtUtc,
            Revision = row.Revision,
            CleanupDecisionCode = row.CleanupDecisionCode,
            CleanupDecisionAtUtc = row.CleanupDecisionAtUtc
        };

        Validate(operation);
        return operation;
    }

    private static void Validate(
        AdvertisementImageUploadOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.UploadOperationId == Guid.Empty ||
            operation.AdvertisementId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Invalid recovery operation identity.");
        }

        if (string.IsNullOrWhiteSpace(operation.AdministratorId) ||
            operation.AdministratorId.Length > 128)
        {
            throw new InvalidOperationException(
                "Invalid trusted Administrator identity.");
        }

        if (!AdvertisementUrlValidator.IsValidImageUrl(
                operation.PreviousImageUrl) ||
            operation.PreviousImageUrl.Length > 2048)
        {
            throw new InvalidOperationException(
                "Invalid previous advertisement image URL.");
        }

        if (operation.ReplacementImageUrl is not null &&
            (!AdvertisementUrlValidator.IsValidImageUrl(
                 operation.ReplacementImageUrl) ||
             operation.ReplacementImageUrl.Length > 2048))
        {
            throw new InvalidOperationException(
                "Invalid replacement advertisement image URL.");
        }

        if (operation.ProviderAssetId is not null)
        {
            var expectedId =
                $"wayhunter/advertisements/{operation.AdvertisementId:D}/" +
                $"{operation.UploadOperationId:N}";

            if (!string.Equals(
                    expectedId,
                    operation.ProviderAssetId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Provider asset ID does not match the operation.");
            }
        }

        if (operation.ReplacementImageUrl is not null &&
            operation.ProviderAssetId is null)
        {
            throw new InvalidOperationException(
                "Replacement URL requires a reserved provider asset.");
        }

        if (!Enum.IsDefined(typeof(AdvertisementImageRecoveryState),
                operation.State) ||
            operation.RetryCount < 0 ||
            operation.Revision < 0)
        {
            throw new InvalidOperationException(
                "Invalid recovery state, retry count or revision.");
        }

        if (operation.CreatedAtUtc == default ||
            operation.UpdatedAtUtc < operation.CreatedAtUtc ||
            (operation.CompletedAtUtc is { } completed &&
                completed < operation.CreatedAtUtc))
        {
            throw new InvalidOperationException(
                "Invalid recovery operation timestamps.");
        }

        if (operation.ClaimToken.HasValue !=
            operation.LeaseExpiresAtUtc.HasValue ||
            operation.ClaimToken == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Invalid recovery claim and lease pair.");
        }

        if ((operation.LastErrorCode?.Length ?? 0) > 128 ||
            (operation.CleanupDecisionCode?.Length ?? 0) > 128)
        {
            throw new InvalidOperationException(
                "Recovery metadata exceeds approved provisional limits.");
        }
    }
}