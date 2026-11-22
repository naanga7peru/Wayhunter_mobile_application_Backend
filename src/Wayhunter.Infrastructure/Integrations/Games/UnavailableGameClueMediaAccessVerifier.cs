using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Infrastructure.Integrations.Games;

/// <summary>
/// Fail-closed adapter used until an approved private-media
/// ownership provider is configured. Text-only game clues do
/// not invoke this verifier; every media asset is rejected.
/// </summary>
public sealed class UnavailableGameClueMediaAccessVerifier
    : IGameClueMediaAccessVerifier
{
    public Task<bool> IsUsableCreatorClueMediaAsync(
        string mediaAssetId,
        string creatorUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
