using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Application.Modules.Games.Services;

/// <summary>
/// Starts or resumes an attempt using the caller's trusted account ID and
/// server UTC time. The HTTP layer must resolve and validate that account.
/// </summary>
public sealed class StartGameAttemptService
{
    private readonly IStartGameAttemptStore _store;
    private readonly TimeProvider _timeProvider;

    public StartGameAttemptService(
        IStartGameAttemptStore store,
        TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<StartGameAttemptResultDto> ExecuteAsync(
        Guid gameId,
        string playerUserId,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID is required.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(playerUserId))
            throw new ArgumentException("Player ID is required.", nameof(playerUserId));

        return _store.StartOrResumeAsync(
            gameId,
            playerUserId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
