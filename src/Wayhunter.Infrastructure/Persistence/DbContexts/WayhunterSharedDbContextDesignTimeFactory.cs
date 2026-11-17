using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wayhunter.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Supplies provider metadata for migration generation only.
/// No connection string is configured, so design-time tooling cannot
/// select or contact an application database through this factory.
/// </summary>
public sealed class WayhunterSharedDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<WayhunterSharedDbContext>
{
    public WayhunterSharedDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer()
            .Options;

        return new WayhunterSharedDbContext(options);
    }
}
