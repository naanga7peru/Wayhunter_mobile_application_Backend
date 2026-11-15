
namespace Wayhunter.Application.Modules.IdentitySocial.Services;

public sealed record LoginLockoutSnapshot(
    LoginLockoutState State,
    long Version);

public interface ILoginLockoutStateStore
{
    // Missing account state must return:
    // State = (0, null), Version = 0.
    Task<LoginLockoutSnapshot> ReadAsync(
        string userId,
        CancellationToken cancellationToken = default);

    // The storage implementation MUST perform an atomic
    // comparison of expectedVersion and update.
    //
    // Return false if another request updated the state.
    // A successful write increments the version by one.
    Task<bool> TryWriteAsync(
        string userId,
        long expectedVersion,
        LoginLockoutState nextState,
        CancellationToken cancellationToken = default);
}
