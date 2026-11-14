
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningServiceCollectionTests
{
    [Fact]
    public void AddTravelPlanningModule_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();

        services.AddTravelPlanningModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<ITripPlanRepository>();

        Assert.IsType<InMemoryTripPlanRepository>(repository);

        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<CreateTripPlanService>());

        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<GetTripPlanService>());

        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<UpdateTripPlanService>());

        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<CancelTripPlanService>());
    }

    [Fact]
    public async Task Repository_ShouldPersistAcrossScopes()
    {
        var services = new ServiceCollection();

        services.AddTravelPlanningModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

        Guid tripId;

        using (var scope = provider.CreateScope())
        {
            var createService = scope.ServiceProvider
                .GetRequiredService<CreateTripPlanService>();

            var request = new CreateTripPlanDto(
                "Jaffna Tour",
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 22),
                15000m,
                "Car",
                3
            );

            var created = await createService.CreateAsync(
                request,
                "user-001");

            tripId = created.TripPlanId;
        }

        using (var scope = provider.CreateScope())
        {
            var getService = scope.ServiceProvider
                .GetRequiredService<GetTripPlanService>();

            var trip = await getService.GetByIdAsync(
                tripId,
                "user-001");

            Assert.NotNull(trip);
            Assert.Equal("Jaffna Tour", trip.TripName);
            Assert.Equal(tripId, trip.TripPlanId);
        }
    }
}
