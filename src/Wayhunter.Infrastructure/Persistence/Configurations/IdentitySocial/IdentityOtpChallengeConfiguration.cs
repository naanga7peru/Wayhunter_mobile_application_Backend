
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class IdentityOtpChallengeConfiguration
    : IEntityTypeConfiguration<IdentityOtpChallenge>
{
    public void Configure(
        EntityTypeBuilder<IdentityOtpChallenge> builder)
    {
        // Proposed schema - requires DB Coordinator review.
        builder.ToTable("IdentityOtpChallenges");

        // Primary Key
        builder.HasKey(otp => otp.ChallengeId);

        builder.Property(otp => otp.ChallengeId)
            .HasMaxLength(36)
            .ValueGeneratedNever()
            .IsRequired();

        // User relationship
        builder.Property(otp => otp.UserId)
            .HasMaxLength(256)
            .IsRequired();

        // Store verifier hash, NEVER plain OTP.
        builder.Property(otp => otp.OtpVerifierHash)
            .HasMaxLength(1024)
            .IsRequired();

        // OTP creation and expiry
        builder.Property(otp => otp.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(otp => otp.ExpiresAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        // Failed attempt tracking
        builder.Property(otp => otp.FailedAttemptCount)
            .IsRequired();

        builder.Property(otp => otp.MaxFailedAttempts)
            .IsRequired();

        // Successful verification timestamp
        builder.Property(otp => otp.ConsumedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // Revocation timestamp
        builder.Property(otp => otp.RevokedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // Query indexes
        builder.HasIndex(otp => otp.UserId);

        builder.HasIndex(otp => otp.ExpiresAtUtc);

        // UserAccount foreign key
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(otp => otp.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
