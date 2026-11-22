using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IPublicGameDetailsQueryStore
{
    Task<PublicGameDetailsDto?> GetPublicGameAsync(
        Guid gameId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
