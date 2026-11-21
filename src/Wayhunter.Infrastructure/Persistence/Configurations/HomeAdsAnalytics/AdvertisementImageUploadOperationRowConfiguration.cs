using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;

/// <summary>
/// Proposed SQL Server mapping, not yet added to the shared model.
/// PK, indexes, FK and state constraints require DB approval.
/// </summary>
public sealed class AdvertisementImageUploadOperationRowConfiguration
    : IEntityTypeConfiguration<AdvertisementImageUploadOperationRow>
{
    public void Configure(
        EntityTypeBuilder<AdvertisementImageUploadOperationRow> builder)
    {
        builder.ToTable("AdvertisementImageUploadOperations", table =>
        {
            table.HasCheckConstraint(
                "CK_AdImageOps_State",
                "[State] BETWEEN 0 AND 8");

            table.HasCheckConstraint(
                "CK_AdImageOps_NonNegative",
                "[RetryCount] >= 0 AND [Revision] >= 0");

            table.HasCheckConstraint(
                "CK_AdImageOps_LeasePair",
                "([ClaimToken] IS NULL AND [LeaseExpiresAtUtc] IS NULL) " +
                "OR ([ClaimToken] IS NOT NULL AND [LeaseExpiresAtUtc] IS NOT NULL)");
        });

        builder.HasKey(x => x.UploadOperationId)
            .HasName("PK_AdvertisementImageUploadOperations");

        builder.Property(x => x.UploadOperationId)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(x => x.AdvertisementId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.AdministratorId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.PreviousImageUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.ReplacementImageUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.ProviderAssetId)
            .HasMaxLength(512);

        builder.Property(x => x.State)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(x => x.NextRetryAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.LastErrorCode)
            .HasMaxLength(128);

        builder.Property(x => x.ClaimToken)
            .HasColumnType("uniqueidentifier");

        builder.Property(x => x.LeaseExpiresAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(x => x.Revision)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Property(x => x.CleanupDecisionCode)
            .HasMaxLength(128);

        builder.Property(x => x.CleanupDecisionAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(x => new
        {
            x.AdvertisementId,
            x.State
        })
        .HasDatabaseName("IX_AdImageOps_Ad_State");

        builder.HasIndex(x => new
        {
            x.State,
            x.NextRetryAtUtc
        })
        .HasDatabaseName("IX_AdImageOps_State_Retry");

        builder.HasIndex(x => new
        {
            x.ClaimToken,
            x.LeaseExpiresAtUtc
        })
        .HasDatabaseName("IX_AdImageOps_Claim_Lease");

        // Provisional nonunique index.
        // Final uniqueness scope requires DB Coordinator approval.
        builder.HasIndex(x => new
        {
            x.Provider,
            x.ProviderAssetId
        })
        .HasFilter("[ProviderAssetId] IS NOT NULL")
        .HasDatabaseName("IX_AdImageOps_Provider_Asset");

        // Intentionally no FK or cascade behavior configured yet.
    }
}