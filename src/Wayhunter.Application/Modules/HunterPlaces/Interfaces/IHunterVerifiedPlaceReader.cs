
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Queries;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

public interface IHunterVerifiedPlaceReader
{
    // Get verified places near the submitted GPS location.
    // The implementation must not silently omit
    // verified places inside the requested radius.
    Task<IReadOnlyList<ExistingVerifiedPlace>>
        GetVerifiedPlacesNearAsync(
            double latitude,
            double longitude,
            double radiusMeters,
            CancellationToken cancellationToken = default);
}
