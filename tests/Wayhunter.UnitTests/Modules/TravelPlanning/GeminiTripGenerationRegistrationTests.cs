
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GeminiTripGenerationRegistrationTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        // No actual Gemini API key is configured.
        // AI remains disabled by default.
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
    public void GeminiProvider_IsRegistered()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var aiProvider = scope.ServiceProvider
            .GetRequiredService<IAiTripGenerationProvider>();

        Assert.IsType<GeminiTripGenerationProvider>(
            aiProvider);
    }

    [Fact]
    public void GenerationService_IsResolvable()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<GenerateAiTripPlanService>();

        Assert.NotNull(service);
    }

    [Fact]
    public async Task DisabledGemini_DoesNotGenerateTrip()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<GenerateAiTripPlanService>();

        var request = new AiTripItineraryRequest(
            "Jaffna",
            "Nallur",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 21),
            10000m,
            "Car",
            2,
            null);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GenerateAsync(request));

        Assert.Equal(
            "AI trip planning is disabled.",
            exception.Message);
    }
}
