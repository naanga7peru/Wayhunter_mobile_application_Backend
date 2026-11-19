
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class HistoricalEvidenceRuntimeWiringTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().Build());

        services.AddTravelPlanningModule();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }

    [Fact]
    public void HistoricalReader_DefaultsToFailClosed()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var reader = scope.ServiceProvider
            .GetRequiredService<ITrustedHistoricalEvidenceReader>();

        Assert.IsType<UnavailableTrustedHistoricalEvidenceReader>(
            reader);

        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<GetHistoricalRouteSegmentsService>());
    }

    [Fact]
    public async Task ExistingTrip_WithoutTrustedM3Evidence_ReturnsEmpty()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var trip = new TripPlan(
            "owner-01",
            "Jaffna Tour",
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 16),
            10000m,
            "Car",
            2);

        trip.AddStop("Jaffna");
        trip.AddStop("Nallur");

        var repository = scope.ServiceProvider
            .GetRequiredService<ITripPlanRepository>();

        await repository.AddAsync(trip);

        var service = scope.ServiceProvider
            .GetRequiredService<GetHistoricalRouteSegmentsService>();

        var result = await service.GetAsync(
            trip.Id, "viewer-01");

        Assert.Empty(result);
    }
}
