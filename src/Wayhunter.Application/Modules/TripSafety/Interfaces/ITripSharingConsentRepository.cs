
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Dtos;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

// Authorization and consent changes must be atomic.
// Never perform an unchecked read followed by
// an unguarded update.
public interface ITripSharingConsentRepository
{
    Task<GpsConsentDto> SetMyConsentAsync(
        Guid tripPlanId,
        Guid sessionId,
        string authenticatedUserId,
        bool grant,
        DateTimeOffset serverTimeUtc,
        CancellationToken cancellationToken = default);
}
