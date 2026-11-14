
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class LiveTripSessionConfiguration
    : IEntityTypeConfiguration<LiveTripSession>
{
    public void Configure(
        EntityTypeBuilder<LiveTripSession> builder)
    {
        builder.ToTable(
            "TripSafety_LiveTripSessions",
            table => table.HasCheckConstraint(
                "CK_TripSafety_LiveTripSessions_Status",
                "[Status] IN (1, 2, 3)"));

        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.SessionId)
            .ValueGeneratedNever();

        // Temporary string mapping.
        // M2 TripPlan.Id is Guid; contract review required.
        builder.Property(x => x.TripPlanId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.StartedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.FinishedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.HasIndex(x => x.TripPlanId)
            .IsUnique()
            .HasDatabaseName(
                "UX_TripSafety_LiveTripSessions_OpenPlan")
            .HasFilter("[Status] IN (1, 2)");
    }
}
