using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripMemoryServiceCollectionExtensionsTests
{
    private static IConfiguration Configuration(bool enabled) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TripSafety:EnableSqlTripMemories"] = enabled.ToString(),
            ["ConnectionStrings:WayhunterSharedDb"] = "Server=localhost;Database=MemoryDi;Integrated Security=True;TrustServerCertificate=True"
        })
        .Build();

    [Fact]
    public void SqlDisabled_KeepsMemoryAndMediaUnavailable()
    {
        var services = new ServiceCollection();
        services.AddTripMemoryServices(Configuration(false));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.False(scope.ServiceProvider.GetRequiredService<ITripMemoryStore>().IsAvailable);
        Assert.Contains("UnavailableTripMemoryDependencies", services.Single(x => x.ServiceType == typeof(ITripMemoryMediaProvider)).ImplementationType!.Name);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(DbContextOptions<WayhunterSharedDbContext>));
    }

    [Fact]
    public void SqlEnabled_UsesEfMemoryStoreButLeavesMediaUnavailable()
    {
        var services = new ServiceCollection();
        services.AddTripMemoryServices(Configuration(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<EfTripMemoryStore>(scope.ServiceProvider.GetRequiredService<ITripMemoryStore>());
        Assert.True(scope.ServiceProvider.GetRequiredService<ITripMemoryStore>().IsAvailable);
        Assert.Contains("UnavailableTripMemoryDependencies", services.Single(x => x.ServiceType == typeof(ITripMemoryMediaProvider)).ImplementationType!.Name);
        Assert.True(scope.ServiceProvider.GetRequiredService<WayhunterSharedDbContext>().Database.IsSqlServer());
    }
}
