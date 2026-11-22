using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Api.Services.Games;

/// <summary>Fail-closed runtime provider until the approved durable M5 attempt store is configured.</summary>
public sealed class UnavailableGameAttemptStore : IStartGameAttemptStore
{
    public Task<StartGameAttemptResultDto> StartOrResumeAsync(
        Guid gameId,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromException<StartGameAttemptResultDto>(
            new GameAttemptPersistenceUnavailableException("Durable Game attempt persistence is not configured."));
}
