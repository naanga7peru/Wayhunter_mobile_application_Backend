using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

public interface IPlaceDetailsProvider
{
    Task<PlacesProviderResult> GetDetailsAsync(
        string providerPlaceId, CancellationToken cancellationToken = default);
}
