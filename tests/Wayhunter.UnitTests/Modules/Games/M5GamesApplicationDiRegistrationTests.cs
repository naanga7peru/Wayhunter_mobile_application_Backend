using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class M5GamesApplicationDiRegistrationTests
{
    [Fact]
    public async Task AddGamesApplicationServices_ResolvesWorkflowServicesAndSharedStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddGamesApplicationServices();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var store = scope.ServiceProvider
            .GetRequiredService<InMemoryGameSubmissionStore>();

        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IGameRepository>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IGameSubmissionStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IGameApprovalStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IGameModerationStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IStartGameAttemptStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<ISubmitGameStageProofStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IGameStageProofDecisionStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IGamePendingStageProofQueryStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IAvailableGamesQueryStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<ICurrentGameStageQueryStore>());
        Assert.Same(store, scope.ServiceProvider
            .GetRequiredService<IExpiredGameArchiveStore>());

        Assert.IsType<GameService>(scope.ServiceProvider
            .GetRequiredService<GameService>());
        Assert.IsType<GameDraftDeletionService>(scope.ServiceProvider
            .GetRequiredService<GameDraftDeletionService>());
        Assert.IsType<GameSubmissionService>(scope.ServiceProvider
            .GetRequiredService<GameSubmissionService>());
        Assert.IsType<GameApprovalService>(scope.ServiceProvider
            .GetRequiredService<GameApprovalService>());
        Assert.IsType<GameModerationService>(scope.ServiceProvider
            .GetRequiredService<GameModerationService>());
        Assert.IsType<GetPendingGameStageProofsService>(scope.ServiceProvider
            .GetRequiredService<GetPendingGameStageProofsService>());
        Assert.IsType<ArchiveExpiredGamesService>(scope.ServiceProvider
            .GetRequiredService<ArchiveExpiredGamesService>());
        Assert.IsType<GameClueMediaOwnershipService>(scope.ServiceProvider
            .GetRequiredService<GameClueMediaOwnershipService>());
        Assert.IsType<VerifiedGameDraftService>(scope.ServiceProvider
            .GetRequiredService<VerifiedGameDraftService>());
        Assert.False(await scope.ServiceProvider
            .GetRequiredService<IGameClueMediaAccessVerifier>()
            .IsUsableCreatorClueMediaAsync(
                "unverified-media",
                "creator-001"));
    }

    [Fact]
    public async Task VerifiedDraftDi_AllowsTextOnlyAndRejectsUnverifiedMedia()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddGamesApplicationServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var drafts = scope.ServiceProvider
            .GetRequiredService<VerifiedGameDraftService>();

        var textOnlyDraft = new CreateGameDraftDto(
            "DI verified draft",
            null,
            30,
            new[] { new GameDraftStageDto(1, "Find the gate", null) });

        var created = await drafts.CreateDraftAsync(
            "creator-001",
            textOnlyDraft);

        Assert.Equal("creator-001", created.CreatorUserId);

        var mediaDraft = new CreateGameDraftDto(
            "Unverified media draft",
            null,
            30,
            new[] { new GameDraftStageDto(1, null, "client-media-id") });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => drafts.CreateDraftAsync("creator-001", mediaDraft));
    }

    [Fact]
    public void ConfiguredGamesPersistence_DoesNotEnableExpiryWorkerByDefault()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddGamesApplicationServices();
        services.AddConfiguredGamesPersistence(configuration);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(
                Microsoft.Extensions.Hosting.IHostedService));
    }

    [Fact]
    public void ConfiguredGamesPersistence_EnablesExpiryWorkerOnlyWithBothFlags()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Games:EnableSqlPersistence"] = "true",
                ["Games:EnableExpiryWorker"] = "true",
                ["ConnectionStrings:WayhunterSharedDb"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=WayhunterGameExpiryDiTest;Trusted_Connection=True"
            })
            .Build();

        services.AddGamesApplicationServices();
        services.AddConfiguredGamesPersistence(configuration);

        Assert.Single(services, descriptor => descriptor.ServiceType ==
            typeof(Microsoft.Extensions.Hosting.IHostedService));
    }
}
