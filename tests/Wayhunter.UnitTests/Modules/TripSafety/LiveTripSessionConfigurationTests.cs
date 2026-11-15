
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Xunit;

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

        // Primary key
        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.SessionId)
            .ValueGeneratedNever();

        // M2 TripPlan.Id and M3 TripPlanId use Guid.
        builder.Property(x => x.TripPlanId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // Session status
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        // Start timestamp
        builder.Property(x => x.StartedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        // Finish timestamp
        builder.Property(x => x.FinishedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // M2/M3 foreign key relationship.
        //
        // Principal: Trips.Id
        // Dependent: TripSafety_LiveTripSessions.TripPlanId
        //
        // One TripPlan can have multiple historical
        // LiveTripSessions.
        //
        // NoAction is proposed and requires
        // M2 / DB Coordinator approval.

        builder.HasOne<TripPlan>()
            .WithMany()
            .HasForeignKey(x => x.TripPlanId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_LiveTripSessions_Trips_TripPlanId");

        // Only one Active or Paused session
        // is allowed per TripPlanId.

        builder.HasIndex(x => x.TripPlanId)
            .IsUnique()
            .HasDatabaseName(
                "UX_TripSafety_LiveTripSessions_OpenPlan")
            .HasFilter("[Status] IN (1, 2)");
    // EF Core metadata tests only.
    // No real database connection or migration.

    private sealed class MappingTestDbContext : DbContext
    {
        public MappingTestDbContext(
            DbContextOptions<MappingTestDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(
                new TripPlanConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripStopConfiguration());

            modelBuilder.ApplyConfiguration(
                new LiveTripSessionConfiguration());
        }
    }

    private static MappingTestDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<MappingTestDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_M3_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new MappingTestDbContext(options);
    }

    [Fact]
    public void Should_Map_Correct_Table_Name()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        Assert.Equal(
            "TripSafety_LiveTripSessions",
            entity.GetTableName());
    }

    [Fact]
    public void Should_Configure_SessionId_As_Primary_Key()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        var property = Assert.Single(
            primaryKey.Properties);

        Assert.Equal(
            nameof(LiveTripSession.SessionId),
            property.Name);

        Assert.Equal(typeof(Guid), property.ClrType);
    }

    [Fact]
    public void Should_Configure_TripPlanId_As_Required_Guid()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var property = entity.FindProperty(
            nameof(LiveTripSession.TripPlanId));

        Assert.NotNull(property);

        Assert.Equal(typeof(Guid), property.ClrType);
        Assert.False(property.IsNullable);

        Assert.Equal(
            "uniqueidentifier",
            property.GetColumnType());

        Assert.Null(property.GetMaxLength());
    }

    [Fact]
    public void Should_Map_Timestamps_As_Datetimeoffset()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var started = entity.FindProperty(
            nameof(LiveTripSession.StartedAtUtc));

        var finished = entity.FindProperty(
            nameof(LiveTripSession.FinishedAtUtc));

        Assert.NotNull(started);
        Assert.NotNull(finished);

        Assert.Equal(
            "datetimeoffset",
            started.GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            finished.GetColumnType());

        Assert.False(started.IsNullable);
        Assert.True(finished.IsNullable);
    }

    [Fact]
    public void Should_Configure_Filtered_Unique_Index()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var index = Assert.Single(entity.GetIndexes());

        Assert.True(index.IsUnique);

        Assert.Equal(
            nameof(LiveTripSession.TripPlanId),
            Assert.Single(index.Properties).Name);

        Assert.Equal(
            "UX_TripSafety_LiveTripSessions_OpenPlan",
            index.GetDatabaseName());

        Assert.Equal(
            "[Status] IN (1, 2)",
            index.GetFilter());
    }

    [Fact]
    public void Should_Configure_M2_M3_Foreign_Key()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var foreignKey = Assert.Single(
            entity.GetForeignKeys());

        Assert.Equal(
            typeof(TripPlan),
            foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            nameof(LiveTripSession.TripPlanId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            nameof(TripPlan.Id),
            Assert.Single(
                foreignKey.PrincipalKey.Properties).Name);

        Assert.True(foreignKey.IsRequired);
    }

    [Fact]
    public void Should_Use_NoAction_Delete_Behavior()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var foreignKey = Assert.Single(
            entity.GetForeignKeys());

        // Proposed behavior; requires team approval.
        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Should_Configure_Foreign_Key_Name()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var foreignKey = Assert.Single(
            entity.GetForeignKeys());

        Assert.Equal(
            "FK_TripSafety_LiveTripSessions_Trips_TripPlanId",
            foreignKey.GetConstraintName());
    }

    [Fact]
    public void Should_Map_M2_Principal_To_Trips_Table()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(TripPlan));

        Assert.NotNull(entity);

        Assert.Equal("Trips", entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        var property = Assert.Single(
            primaryKey.Properties);

        Assert.Equal(nameof(TripPlan.Id), property.Name);
        Assert.Equal(typeof(Guid), property.ClrType);
    }
}
