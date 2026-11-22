using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class UnavailableGamesReadQueryStores :
    IAvailableGamesQueryStore,
    IPublicGameDetailsQueryStore,
    IMyGameAttemptsQueryStore
{
    private static GamesReadDependencyUnavailableException Unavailable() => new();

    public Task<IReadOnlyList<PublicGameSummaryDto>> GetAvailableGamesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<PublicGameSummaryDto>>(Unavailable());

    public Task<PublicGameDetailsDto?> GetPublicGameAsync(
        Guid gameId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromException<PublicGameDetailsDto?>(Unavailable());

    public Task<IReadOnlyList<MyGameAttemptDto>> GetMyAttemptsAsync(
        string playerUserId,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<MyGameAttemptDto>>(Unavailable());
}
