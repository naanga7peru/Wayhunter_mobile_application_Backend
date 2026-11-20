
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.HistoricalRoutes;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

// Fail-closed implementation until M3 has an approved
// authoritative completed-trip and verified-visit source.
public sealed class UnavailableTrustedHistoricalEvidenceReader
    : ITrustedHistoricalEvidenceReader
{
    public Task<M3VerifiedHistoricalEvidence?> GetForViewerAsync(
        Guid tripPlanId,
        string authenticatedViewerUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // No verified history must never become fabricated history.
        return Task.FromResult<M3VerifiedHistoricalEvidence?>(null);
    }
}
