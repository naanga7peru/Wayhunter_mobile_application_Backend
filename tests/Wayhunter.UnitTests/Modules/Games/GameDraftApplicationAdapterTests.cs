using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Controllers.Mobile.Games;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Integrations.Games;
using Wayhunter.Api.Extensions;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameDraftApplicationAdapterTests
{
    [Fact]
    public async Task DraftLifecycle_UsesResolvedOwnerAndEnforcesDraftState()
    {
        var owner = CreateActiveAccount();
        var other = CreateActiveAccount();
        using var provider = CreateProvider(owner, other);
        var adapter = CreateAdapter(provider);
        var ownerPrincipal = Principal(owner.UserId);
        var otherPrincipal = Principal(other.UserId);

        var created = await adapter.CreateAsync(ownerPrincipal, TextDraft("First"));

        Assert.Equal(owner.UserId, created.CreatorUserId);
        Assert.Equal("First", created.Title);
        Assert.Same(created, await adapter.ReadAsync(ownerPrincipal, created.Id));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.ReadAsync(otherPrincipal, created.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.UpdateAsync(otherPrincipal, created.Id, TextDraft("Intrusion")));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.DeleteAsync(otherPrincipal, created.Id));

        var updated = await adapter.UpdateAsync(
            ownerPrincipal,
            created.Id,
            TextDraft("Updated"));
        Assert.Equal("Updated", updated.Title);

        await adapter.DeleteAsync(ownerPrincipal, created.Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.ReadAsync(ownerPrincipal, created.Id));
    }

    [Fact]
    public async Task Create_RejectsUnauthenticatedOrInactiveIdentity()
    {
        var active = CreateActiveAccount();
        var inactive = UserAccount.RegisterPending(
            "Pending", "pending@example.test", "hash", DateTimeOffset.UtcNow);
        using var provider = CreateProvider(active, inactive);
        var adapter = CreateAdapter(provider);

        await Assert.ThrowsAsync<GameDraftIdentityRejectedException>(
            () => adapter.CreateAsync(new ClaimsPrincipal(), TextDraft("No principal")));
        await Assert.ThrowsAsync<GameDraftIdentityRejectedException>(
            () => adapter.CreateAsync(Principal(inactive.UserId), TextDraft("Pending")));
    }

    [Fact]
    public async Task Create_ValidatesDraftAndFailsClosedForUnverifiedMedia()
    {
        var active = CreateActiveAccount();
        using var provider = CreateProvider(active);
        var adapter = CreateAdapter(provider);

        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.CreateAsync(
                Principal(active.UserId),
                new CreateGameDraftDto(" ", null, 30,
                    [new GameDraftStageDto(1, "Hint", null)])));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => adapter.CreateAsync(
                Principal(active.UserId),
                new CreateGameDraftDto("Media", null, 30,
                    [new GameDraftStageDto(1, null, "unverified-media")])));
    }

    private static ServiceProvider CreateProvider(params UserAccount[] accounts)
    {
        var services = new ServiceCollection();
        services.AddGamesApplicationServices();
        services.AddSingleton<ITrustedIdentityAccountReader>(
            new TestAccountReader(accounts));
        services.AddSingleton<TrustedIdentityResolutionService>();
        return services.BuildServiceProvider();
    }

    private static GameDraftApplicationAdapter CreateAdapter(
        IServiceProvider provider) => new(
            provider.GetRequiredService<TrustedIdentityResolutionService>(),
            provider.GetRequiredService<VerifiedGameDraftService>(),
            provider.GetRequiredService<GameService>(),
            provider.GetRequiredService<GameDraftDeletionService>());

    private static UserAccount CreateActiveAccount()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.RegisterPending(
            "Test user", $"{Guid.NewGuid():N}@example.test", "hash", now);
        account.ActivateAfterVerifiedOtp(now.AddSeconds(1));
        return account;
    }

    private static ClaimsPrincipal Principal(string userId) => new(
        new ClaimsIdentity(
            [new Claim("sub", userId)],
            authenticationType: "unit-test"));

    private static CreateGameDraftDto TextDraft(string title) => new(
        title,
        null,
        30,
        [new GameDraftStageDto(1, "Text clue", null)]);

    private sealed class TestAccountReader(UserAccount[] accounts)
        : ITrustedIdentityAccountReader
    {
        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(accounts.SingleOrDefault(
                account => account.UserId == userId));
        }
    }
}
