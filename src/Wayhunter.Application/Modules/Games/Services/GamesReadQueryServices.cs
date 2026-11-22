using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class GamesReadDependencyUnavailableException : Exception
{
    public GamesReadDependencyUnavailableException()
        : base("The required Games read dependency is unavailable.") { }
}

public sealed class GetAvailableGamesQueryService
{
    private readonly IAvailableGamesQueryStore _store;
    private readonly TimeProvider _timeProvider;

    public GetAvailableGamesQueryService(
        IAvailableGamesQueryStore store,
        TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<IReadOnlyList<PublicGameSummaryDto>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteStoreAsync(() => _store.GetAvailableGamesAsync(
            _timeProvider.GetUtcNow(), cancellationToken), cancellationToken);

    internal static async Task<T> ExecuteStoreAsync<T>(
        Func<Task<T>> query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await query();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GamesReadDependencyUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new GamesReadDependencyUnavailableException();
        }
    }
}

public sealed class GetPublicGameDetailsQueryService
{
    private readonly IPublicGameDetailsQueryStore _store;
    private readonly TimeProvider _timeProvider;

    public GetPublicGameDetailsQueryService(
        IPublicGameDetailsQueryStore store,
        TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<PublicGameDetailsDto?> ExecuteAsync(
        Guid gameId,
        CancellationToken cancellationToken = default) =>
        GetAvailableGamesQueryService.ExecuteStoreAsync(
            () => _store.GetPublicGameAsync(
                gameId, _timeProvider.GetUtcNow(), cancellationToken),
            cancellationToken);
}

public sealed class GetMyGameAttemptsQueryService
{
    private readonly IMyGameAttemptsQueryStore _store;

    public GetMyGameAttemptsQueryService(IMyGameAttemptsQueryStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<MyGameAttemptDto>> ExecuteAsync(
        string playerUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerUserId))
            throw new ArgumentException("Player ID is required.", nameof(playerUserId));

        return GetAvailableGamesQueryService.ExecuteStoreAsync(
            () => _store.GetMyAttemptsAsync(playerUserId, cancellationToken),
            cancellationToken);
    }
}
