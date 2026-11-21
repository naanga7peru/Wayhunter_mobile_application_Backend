
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.HistoricalRoutes;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

// M3 must implement this with trusted server-side evidence.
// Never populate evidence from unverified mobile input.
public interface ITrustedHistoricalEvidenceReader
{
    Task<M3VerifiedHistoricalEvidence?> GetForViewerAsync(
        Guid tripPlanId,
        string authenticatedViewerUserId,
        CancellationToken cancellationToken = default);
}
