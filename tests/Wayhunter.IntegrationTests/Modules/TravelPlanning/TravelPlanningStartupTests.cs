
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Net;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class TravelPlanningStartupTests
{
    private const string TestConnectionString =
        "Server=localhost;Database=Wayhunter_M2_Test;" +
        "Trusted_Connection=True;TrustServerCertificate=True;";

    private static WebApplicationFactory<Program> CreateFactory(
        bool enableSql,
        string? connectionString = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    if (!enableSql)
                    {
                        services.Replace(
                            ServiceDescriptor.Singleton<
                                ITripPlanRepository,
                                InMemoryTripPlanRepository>());

                        return;
                    }

                    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(
                            new System.Collections.Generic.Dictionary<
                                string, string?>
                            {
                                ["ConnectionStrings:WayhunterSharedDb"] =
                                    connectionString
                            })
                        .Build();

                    services.AddTravelPlanningSqlPersistence(
                        configuration);
                });
            });
    }

    [Fact]
    public async Task ApiStartup_ShouldRegisterTravelPlanningServices()
    {
        using var factory = CreateFactory(enableSql: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(
            services.GetRequiredService<ITripPlanRepository>());

        Assert.NotNull(
            services.GetRequiredService<CreateTripPlanService>());

        Assert.NotNull(
            services.GetRequiredService<GetTripPlanService>());

        Assert.NotNull(
            services.GetRequiredService<UpdateTripPlanService>());

        Assert.NotNull(
            services.GetRequiredService<CancelTripPlanService>());
    }

    [Fact]
    public void SqlFlagDisabled_ShouldUseInMemoryRepository()
    {
        using var factory = CreateFactory(enableSql: false);
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<ITripPlanRepository>();

        Assert.IsType<InMemoryTripPlanRepository>(repository);
    }

    [Fact]
    public void SqlFlagEnabled_ShouldUseEfRepository()
    {
        using var factory = CreateFactory(
            enableSql: true,
            connectionString: TestConnectionString);

        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<ITripPlanRepository>();

        Assert.IsType<EfTripPlanRepository>(repository);

        var dbContext = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void SqlFlagEnabled_WithoutConnection_ShouldFailStartup()
    {
        using var factory = CreateFactory(enableSql: true);

        var exception = Assert.Throws<InvalidOperationException>(
            () =>
            {
                using var client = factory.CreateClient();
            });

        Assert.Contains(
            "WayhunterSharedDb",
            exception.Message);
    }
}
