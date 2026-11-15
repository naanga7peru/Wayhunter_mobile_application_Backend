
using Wayhunter.Application.Modules.IdentitySocial.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class LoginLockoutStateCoordinatorTests
{
    private static readonly string UserId =
        Guid.NewGuid().ToString("D");

    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly LoginLockoutOptions Options =
        new(5, TimeSpan.FromMinutes(15));

    private sealed class FakeStore : ILoginLockoutStateStore
    {
        private readonly object _gate = new();

        private LoginLockoutState _state = new(0, null);
        private long _version;

        public bool ConflictNextWrite { get; set; }

        public int Writes { get; private set; }

        public Task<LoginLockoutSnapshot> ReadAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                return Task.FromResult(
                    new LoginLockoutSnapshot(
                        _state,
                        _version));
            }
        }

        public Task<bool> TryWriteAsync(
            string userId,
            long expectedVersion,
            LoginLockoutState nextState,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (ConflictNextWrite)
                {
                    ConflictNextWrite = false;
                    _version++;
                    return Task.FromResult(false);
                }

                if (_version != expectedVersion)
                    return Task.FromResult(false);

                _state = nextState;
                _version++;
                Writes++;

                return Task.FromResult(true);
            }
        }

        public void Seed(LoginLockoutState state)
        {
            lock (_gate)
            {
                _state = state;
                _version++;
            }
        }

        public LoginLockoutSnapshot Snapshot()
        {
            lock (_gate)
            {
                return new LoginLockoutSnapshot(
                    _state, _version);
            }
        }
    }

    [Fact]
    public async Task FirstFailure_IsPersisted()
    {
        var store = new FakeStore();
        var coordinator = new LoginLockoutStateCoordinator(store);

        var result = await coordinator.RecordFailureAsync(
            UserId, Options, Now);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.State.FailedAttempts);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task FifthFailure_ActivatesLockout()
    {
        var store = new FakeStore();
        var coordinator = new LoginLockoutStateCoordinator(store);

        LoginLockoutUpdateResult? result = null;

        for (var i = 0; i < 5; i++)
        {
            result = await coordinator.RecordFailureAsync(
                UserId, Options, Now);
        }

        Assert.NotNull(result);
        Assert.False(result.IsAllowed);
        Assert.Equal(5, result.State.FailedAttempts);
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            result.RetryAfter);
        Assert.Equal(5, store.Writes);
    }

    [Fact]
    public async Task LockedAccount_DoesNotChangeState()
    {
        var store = new FakeStore();

        store.Seed(new LoginLockoutState(
            5, Now.AddMinutes(15)));

        var coordinator = new LoginLockoutStateCoordinator(store);

        var result = await coordinator.RecordFailureAsync(
            UserId, Options, Now.AddMinutes(3));

        Assert.False(result.IsAllowed);
        Assert.Equal(0, store.Writes);
        Assert.Equal(
            Now.AddMinutes(15),
            result.State.LockedUntilUtc);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailures()
    {
        var store = new FakeStore();
        store.Seed(new LoginLockoutState(3, null));

        var coordinator = new LoginLockoutStateCoordinator(store);

        var result = await coordinator.RecordSuccessAsync(
            UserId, Now);

        Assert.True(result.IsAllowed);
        Assert.Equal(0, result.State.FailedAttempts);
        Assert.Null(result.State.LockedUntilUtc);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task ActiveLockout_CannotBeReset()
    {
        var store = new FakeStore();
        store.Seed(new LoginLockoutState(
            5, Now.AddMinutes(15)));

        var coordinator = new LoginLockoutStateCoordinator(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.RecordSuccessAsync(UserId, Now));

        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task VersionConflict_IsRetried()
    {
        var store = new FakeStore
        {
            ConflictNextWrite = true
        };

        var coordinator = new LoginLockoutStateCoordinator(store);

        var result = await coordinator.RecordFailureAsync(
            UserId, Options, Now);

        Assert.Equal(1, result.State.FailedAttempts);
        Assert.Equal(1, store.Writes);
        Assert.Equal(2, store.Snapshot().Version);
    }

    [Fact]
    public async Task ConcurrentFailures_DoNotLoseUpdates()
    {
        var store = new FakeStore();
        var coordinator = new LoginLockoutStateCoordinator(store);

        await Task.WhenAll(
            coordinator.RecordFailureAsync(
                UserId, Options, Now),
            coordinator.RecordFailureAsync(
                UserId, Options, Now));

        Assert.Equal(
            2,
            store.Snapshot().State.FailedAttempts);
    }

    [Fact]
    public async Task CheckLockedAccount_ReturnsRetryAfter()
    {
        var store = new FakeStore();
        store.Seed(new LoginLockoutState(
            5, Now.AddMinutes(15)));

        var coordinator = new LoginLockoutStateCoordinator(store);

        var result = await coordinator.CheckAsync(
            UserId, Now.AddMinutes(5));

        Assert.False(result.IsAllowed);
        Assert.Equal(
            TimeSpan.FromMinutes(10),
            result.RetryAfter);
    }

    [Fact]
    public async Task InvalidUserId_IsRejected()
    {
        var coordinator =
            new LoginLockoutStateCoordinator(new FakeStore());

        await Assert.ThrowsAsync<ArgumentException>(
            () => coordinator.RecordFailureAsync(
                "invalid-user",
                Options,
                Now));
    }

    [Fact]
    public async Task CancelledRequest_IsRejected()
    {
        var coordinator =
            new LoginLockoutStateCoordinator(new FakeStore());

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.RecordFailureAsync(
                UserId,
                Options,
                Now,
                cancellation.Token));
    }

    [Fact]
    public void NullStore_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoginLockoutStateCoordinator(null!));
    }
}
