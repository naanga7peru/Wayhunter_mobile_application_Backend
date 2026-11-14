
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ITripSessionRecoveryAccessChecker
{
    // Must validate authenticated identity and
    // permitted trip ownership or membership.
    // Never authorize using only a client-provided UserId.
    Task<bool> CanRecoverAsync(
        string authenticatedUserId,
        string tripPlanId,
        CancellationToken cancellationToken = default);
}
