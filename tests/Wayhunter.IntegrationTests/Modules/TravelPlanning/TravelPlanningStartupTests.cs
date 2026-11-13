
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class TravelPlanningStartupTests
{
    [Fact]
    public async Task ApiStartup_ShouldRegisterTravelPlanningServices()
    {
        // Start the real ASP.NET Core application in a test host.
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // Check that the HTTP server starts successfully.
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify services registered by Program.cs.
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
}
