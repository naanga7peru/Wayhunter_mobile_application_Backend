using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class UserRoleAssignmentConfiguration
    : IEntityTypeConfiguration<UserRoleAssignmentRow>
{
    public void Configure(
        EntityTypeBuilder<UserRoleAssignmentRow> builder)
    {
        // Approved logical schema.
        // Physical SQL migration is NOT approved.
        builder.ToTable("UserRoleAssignments", table =>
        {
            table.HasCheckConstraint(
                "CK_UserRoleAssignments_Role",
                "[Role] = N'Admin'");

            table.HasCheckConstraint(
                "CK_UserRoleAssignments_Status",
                "[Status] IN (N'Active', N'Revoked', N'Inactive')");

            table.HasCheckConstraint(
                "CK_UserRoleAssignments_Revocation",
                @"(
                    ([Status] = N'Active'
                        AND [RevokedAtUtc] IS NULL
                        AND [RevokedByUserId] IS NULL)
                    OR
                    ([Status] = N'Inactive'
                        AND [RevokedAtUtc] IS NULL
                        AND [RevokedByUserId] IS NULL)
                    OR
                    ([Status] = N'Revoked'
                        AND [RevokedAtUtc] IS NOT NULL
                        AND [RevokedAtUtc] >= [AssignedAtUtc])
                )");

            table.HasCheckConstraint(
                "CK_UserRoleAssignments_AssignmentTime",
                "[AssignedAtUtc] IS NOT NULL");
        });

        builder.HasKey(x => x.AssignmentId)
            .HasName("PK_UserRoleAssignments");

        builder.Property(x => x.AssignmentId)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnType("nvarchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnType("nvarchar(16)")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.AssignedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.AssignedByUserId)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.RevokedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.RevokedByUserId)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256)
            .IsRequired(false);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_UserRoleAssignments_Users_UserId");

        builder.HasIndex(x => new { x.UserId, x.Role })
            .IsUnique()
            .HasFilter("[Status] = N'Active'")
            .HasDatabaseName(
                "UX_UserRoleAssignments_Active_UserId_Role");

        builder.HasIndex(x => new { x.UserId, x.Status })
            .HasDatabaseName(
                "IX_UserRoleAssignments_UserId_Status");

        builder.HasIndex(x => x.AssignedAtUtc)
            .HasDatabaseName(
                "IX_UserRoleAssignments_AssignedAtUtc");
    }
}