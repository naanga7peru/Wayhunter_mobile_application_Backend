
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterActiveAccountAuthorizationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeAccountReader
        : ITrustedIdentityAccountReader
    {
        public UserAccount? Account { get; set; }

        public int ReadCount { get; private set; }

        public string? LastUserId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Action? OnRead { get; set; }

        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadCount++;
            LastUserId = userId;
            LastToken = cancellationToken;

            OnRead?.Invoke();

            return Task.FromResult<UserAccount?>(Account);
        }
    }

    private sealed class FakeSessionStore
        : IHunterCaptureSessionStore
    {
        public int CreateCount { get; private set; }

        public Guid SessionId { get; } = Guid.NewGuid();

        public Guid LastHunterId { get; private set; }

        public Guid LastSubmissionId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<Guid> CreateAsync(
            Guid hunterId,
            Guid hunterSubmissionId,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            LastHunterId = hunterId;
            LastSubmissionId = hunterSubmissionId;
            LastToken = cancellationToken;

            return Task.FromResult(SessionId);
        }

        public Task<bool> TryConsumeAsync(
            Guid captureSessionId,
            Guid authenticatedHunterId,
            Guid hunterSubmissionId,
            DateTimeOffset serverReceivedAtUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private static UserAccount CreateAccount(
        bool active = true)
    {
        var account = UserAccount.RegisterPending(
            "Hunter Test User",
            $"hunter-{Guid.NewGuid():N}@example.com",
            "test-only-password-hash",
            Now);

        if (active)
        {
            account.ActivateAfterVerifiedOtp(
                Now.AddMinutes(5));
        }

        return account;
    }

    private static (
        HunterStartAuthorizedCaptureSessionHandler Handler,
        FakeAccountReader Accounts,
        FakeSessionStore Store,
        Guid SubmissionId)
        CreateHandler(
            string submissionOwnerId,
            UserAccount? account)
    {
        var submissionId = Guid.NewGuid();

        var submission = new HunterSubmission
        {
            Id = submissionId,
            UserId = submissionOwnerId,
            PlaceName = "Test Hunter Place",
            District = "Jaffna"
        };

        // Real in-memory submission reader.
        var submissionReader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { submission });

        var ownershipService =
            new HunterCaptureSubmissionOwnershipService(
                submissionReader);

        // Backend-controlled test account source.
        var accounts = new FakeAccountReader
        {
            Account = account
        };

        // Real M4 trusted identity adapter.
        var trustedResolver =
            new TrustedHunterCaptureSessionIdentityResolver(
                accounts);

        var identityService =
            new HunterCaptureSessionIdentityResolutionService(
                trustedResolver);

        // Fake capture-session persistence.
        var store = new FakeSessionStore();

        var sessionService =
            new HunterCaptureSessionService(
                store,
                TimeProvider.System);

        var captureHandler =
            new HunterStartCaptureSessionHandler(
                sessionService);

        var handler =
            new HunterStartAuthorizedCaptureSessionHandler(
                ownershipService,
                identityService,
                captureHandler);

        return (handler, accounts, store, submissionId);
    }

    private static HunterStartCaptureSessionRequestDto
        CreateRequest(Guid submissionId)
    {
        return new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = submissionId
        };
    }

    [Fact]
    public async Task ActiveVerifiedOwner_ShouldCreateSession()
    {
        var account = CreateAccount();

        var (handler, accounts, store, submissionId) =
            CreateHandler(account.UserId, account);

        var result = await handler.HandleAsync(
            account.UserId,
            CreateRequest(submissionId));

        Assert.Equal(store.SessionId, result.CaptureSessionId);
        Assert.Equal(submissionId, result.HunterSubmissionId);

        Assert.Equal(1, accounts.ReadCount);
        Assert.Equal(1, store.CreateCount);

        Assert.Equal(account.UserId, accounts.LastUserId);
        Assert.Equal(
            Guid.Parse(account.UserId),
            store.LastHunterId);

        Assert.Equal(submissionId, store.LastSubmissionId);
    }

    [Fact]
    public async Task PendingAccount_ShouldNotCreateSession()
    {
        var account = CreateAccount(active: false);

        var (handler, accounts, store, submissionId) =
            CreateHandler(account.UserId, account);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                account.UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task SuspendedAccount_ShouldNotCreateSession()
    {
        var account = CreateAccount();
        account.Suspend();

        var (handler, accounts, store, submissionId) =
            CreateHandler(account.UserId, account);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                account.UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task DeletionRequestedAccount_ShouldNotCreateSession()
    {
        var account = CreateAccount();

        account.RequestDeletion(Now.AddMinutes(10));

        var (handler, accounts, store, submissionId) =
            CreateHandler(account.UserId, account);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                account.UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MissingAccount_ShouldNotCreateSession()
    {
        var userId = Guid.NewGuid().ToString("D");

        var (handler, accounts, store, submissionId) =
            CreateHandler(userId, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                userId,
                CreateRequest(submissionId)));

        Assert.Equal(1, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task WrongSubmissionOwner_ShouldNotReadAccount()
    {
        var account = CreateAccount();
        var differentOwnerId = Guid.NewGuid().ToString("D");

        var (handler, accounts, store, submissionId) =
            CreateHandler(differentOwnerId, account);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                account.UserId,
                CreateRequest(submissionId)));

        Assert.Equal(0, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MismatchedAccountRecord_ShouldNotCreateSession()
    {
        var account = CreateAccount();
        var requestedUserId = Guid.NewGuid().ToString("D");

        var (handler, accounts, store, submissionId) =
            CreateHandler(requestedUserId, account);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                requestedUserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task InvalidGuid_ShouldNotCreateSession()
    {
        const string invalidUserId = "invalid-hunter-id";

        var (handler, accounts, store, submissionId) =
            CreateHandler(invalidUserId, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                invalidUserId,
                CreateRequest(submissionId)));

        Assert.Equal(0, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotCreateSession()
    {
        var account = CreateAccount();

        var (handler, accounts, store, submissionId) =
            CreateHandler(account.UserId, account);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                account.UserId,
                CreateRequest(submissionId),
                cts.Token));

        Assert.Equal(0, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancellationDuringAccountLookup_ShouldStopSession()
    {
        var account = CreateAccount();

        var (handler, accounts, store, submissionId) =
            CreateHandler(account.UserId, account);

        using var cts = new CancellationTokenSource();

        accounts.OnRead = cts.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                account.UserId,
                CreateRequest(submissionId),
                cts.Token));

        Assert.Equal(1, accounts.ReadCount);
        Assert.Equal(0, store.CreateCount);
        Assert.Equal(cts.Token, accounts.LastToken);
    }
}
