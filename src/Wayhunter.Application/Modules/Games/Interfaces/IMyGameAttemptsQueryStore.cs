using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IMyGameAttemptsQueryStore
{
    Task<IReadOnlyList<MyGameAttemptDto>> GetMyAttemptsAsync(
        string playerUserId,
        CancellationToken cancellationToken = default);
}
