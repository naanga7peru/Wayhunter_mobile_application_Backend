
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// M5-MS04-T01
/// Contract for retrieving publicly available games.
/// </summary>
public interface IAvailableGamesQueryStore
{
    // Return only games that are eligible for public play.
    //
    // Required rules:
    // 1. Admin-approved and published.
    // 2. Not expired at server UTC time.
    // 3. Not rejected, disabled or flagged.
    // 4. No verified winner already selected.
    // 5. Return safe public summary fields only.
    //
    // SQL implementation is blocked until
    // the shared migration is approved.

    Task<IReadOnlyList<PublicGameSummaryDto>>
        GetAvailableGamesAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default);
}
