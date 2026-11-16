
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

namespace Wayhunter.Infrastructure.Persistence.DbContexts;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> Users
        => Set<UserAccount>();

    public DbSet<IdentitySession> IdentitySessions
        => Set<IdentitySession>();


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(
            new UserAccountConfiguration());

        modelBuilder.ApplyConfiguration(
            new UserProfileConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentitySessionConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentityOtpChallengeConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentityConsentConfiguration());
    }

}
