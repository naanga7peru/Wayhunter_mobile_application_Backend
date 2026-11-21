using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;

/// <summary>
/// Provisional M7 audit table proposal.
/// Requires DB Coordinator approval before DbContext registration.
/// No FK or cascade is configured until ownership is approved.
/// </summary>
public sealed class AdvertisementImageUploadAuditEventRowConfiguration
    : IEntityTypeConfiguration<AdvertisementImageUploadAuditEventRow>
{
    public void Configure(
        EntityTypeBuilder<AdvertisementImageUploadAuditEventRow> builder)
    {
        builder.ToTable("AdvertisementImageUploadAuditEvents", table =>
        {
            table.HasCheckConstraint(
                "CK_AdImageAudit_PreviousState",
                "[PreviousState] IS NULL OR [PreviousState] BETWEEN 0 AND 8");

            table.HasCheckConstraint(
                "CK_AdImageAudit_NewState",
                "[NewState] IS NULL OR [NewState] BETWEEN 0 AND 8");

            table.HasCheckConstraint(
                "CK_AdImageAudit_EventType",
                "LEN(LTRIM(RTRIM([EventType]))) > 0");
        });

        builder.HasKey(x => x.EventId)
            .HasName("PK_AdvertisementImageUploadAuditEvents");

        builder.Property(x => x.EventId)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(x => x.UploadOperationId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TimestampUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.ActorKind)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ActorId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(128);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(128);

        builder.Property(x => x.PreviousState)
            .HasColumnType("int");

        builder.Property(x => x.NewState)
            .HasColumnType("int");

        builder.HasIndex(x => new
        {
            x.UploadOperationId,
            x.TimestampUtc
        })
        .HasDatabaseName("IX_AdImageAudit_Operation_Time");

        builder.HasIndex(x => new
        {
            x.CorrelationId,
            x.TimestampUtc
        })
        .HasFilter("[CorrelationId] IS NOT NULL")
        .HasDatabaseName("IX_AdImageAudit_Correlation_Time");

        // No cascade and no final FK until DB Coordinator approval.
    }
}