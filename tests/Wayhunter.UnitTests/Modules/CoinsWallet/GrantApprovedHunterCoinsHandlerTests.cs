
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class GrantApprovedHunterCoinsHandlerTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAt =
        SubmittedAt.AddMinutes(10);

    private static HunterApprovedRewardSnapshot Approved(
        Guid? id = null,
        string owner = "hunter-001")
    {
        Guid submissionId = id ?? Guid.NewGuid();

        return new HunterApprovedRewardSnapshot(
            SubmissionId: submissionId,
            HunterUserId: owner,
            SubmissionStatus: HunterSubmissionStatus.Approved,
            SubmittedAtUtc: SubmittedAt,
            ReviewId: Guid.NewGuid(),
            ReviewSubmissionId: submissionId,
            ReviewStatus: HunterSubmissionStatus.Approved,
            ReviewerAdminUserId: "admin-001",
            ReviewedAtUtc: ApprovedAt);
    }

    private static GrantApprovedHunterCoinsHandler Handler(
        FakeReader reader,
        FakeRewardStore store)
    {
        return new GrantApprovedHunterCoinsHandler(
            reader,
            store,
            "2026-11-11");
    }

    [Fact]
    public async Task ApprovedHunter_Receives200Coins()
    {
        var approval = Approved();
        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        bool granted = await handler.HandleAsync(
            approval.SubmissionId,
            Guid.NewGuid());

        Assert.True(granted);

        var wallet = store.Wallet(approval.HunterUserId);
        Assert.NotNull(wallet);

        Assert.Equal(200, wallet.ReadBalance().PostedCoins);
        Assert.Equal(200, wallet.ReadBalance().SpendableCoins);
        Assert.Equal(200,
            wallet.ReadBalance().LifetimeEarnedCoins);
        Assert.Single(wallet.ReadEntries());

        var reward = Assert.Single(store.Requests);

        Assert.Equal(CoinRewardType.HunterApproved,
            reward.RewardType);
        Assert.Equal(200, reward.CoinsAmount);
        Assert.Equal(
            approval.SubmissionId.ToString("D"),
            reward.SourceRecordId);
        Assert.Equal(
            approval.ReviewId.ToString("D"),
            reward.ApprovalRecordId);
        Assert.Equal(
            approval.HunterUserId,
            reward.RecipientUserId);
        Assert.Equal(ApprovedAt,
            reward.EligibilityConfirmedAtUtc);
    }

    [Fact]
    public async Task RepeatedApproval_CreditsOnce()
    {
        var approval = Approved();
        var store = new FakeRewardStore();
        var reader = new FakeReader(approval);
        var handler = Handler(reader, store);

        Assert.True(await handler.HandleAsync(
            approval.SubmissionId, Guid.NewGuid()));

        // Simulate a retried event with a different review ID.
        reader.Snapshot = approval with
        {
            ReviewId = Guid.NewGuid()
        };

        Assert.False(await handler.HandleAsync(
            approval.SubmissionId, Guid.NewGuid()));

        var wallet = store.Wallet(approval.HunterUserId)!;

        Assert.Equal(200, wallet.ReadBalance().PostedCoins);
        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public async Task ConcurrentApprovals_CreditOnlyOnce()
    {
        var approval = Approved();
        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        var attempts = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
                handler.HandleAsync(
                    approval.SubmissionId,
                    Guid.NewGuid())));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(x => x));

        var wallet = store.Wallet(approval.HunterUserId)!;

        Assert.Equal(200, wallet.ReadBalance().PostedCoins);
        Assert.Single(wallet.ReadEntries());
    }

    [Fact]
    public async Task DifferentApprovedSubmissions_EarnSeparately()
    {
        var first = Approved();
        var second = Approved(owner: first.HunterUserId);

        var reader = new FakeReader(first);
        var store = new FakeRewardStore();
        var handler = Handler(reader, store);

        Assert.True(await handler.HandleAsync(
            first.SubmissionId, Guid.NewGuid()));

        reader.Snapshot = second;

        Assert.True(await handler.HandleAsync(
            second.SubmissionId, Guid.NewGuid()));

        var wallet = store.Wallet(first.HunterUserId)!;

        Assert.Equal(400, wallet.ReadBalance().PostedCoins);
        Assert.Equal(2, wallet.ReadEntries().Count);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    [InlineData(HunterSubmissionStatus.Held)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task UnapprovedSubmission_EarnsZero(
        HunterSubmissionStatus status)
    {
        var approval = Approved() with
        {
            SubmissionStatus = status
        };

        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        Assert.False(await handler.HandleAsync(
            approval.SubmissionId, Guid.NewGuid()));

        Assert.Empty(store.Requests);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task UnapprovedReview_EarnsZero(
        HunterSubmissionStatus status)
    {
        var approval = Approved() with
        {
            ReviewStatus = status
        };

        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        Assert.False(await handler.HandleAsync(
            approval.SubmissionId, Guid.NewGuid()));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task MissingSubmission_EarnsZero()
    {
        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(null), store);

        Assert.False(await handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid()));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task WrongReviewReference_IsRejected()
    {
        var approval = Approved() with
        {
            ReviewSubmissionId = Guid.NewGuid()
        };

        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                approval.SubmissionId,
                Guid.NewGuid()));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task NonUtcApproval_IsRejected()
    {
        var approval = Approved() with
        {
            ReviewedAtUtc = ApprovedAt.ToOffset(
                TimeSpan.FromHours(5.5))
        };

        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                approval.SubmissionId,
                Guid.NewGuid()));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task MissingAdminReviewer_IsRejected()
    {
        var approval = Approved() with
        {
            ReviewerAdminUserId = ""
        };

        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                approval.SubmissionId,
                Guid.NewGuid()));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task InvalidRequestIds_AreRejected()
    {
        var approval = Approved();
        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                Guid.Empty, Guid.NewGuid()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                approval.SubmissionId, Guid.Empty));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCredit()
    {
        var approval = Approved();
        var store = new FakeRewardStore();
        var handler = Handler(new FakeReader(approval), store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                approval.SubmissionId,
                Guid.NewGuid(),
                cts.Token));

        Assert.Empty(store.Requests);
    }

    private sealed class FakeReader
        : IHunterApprovedRewardReader
    {
        public HunterApprovedRewardSnapshot? Snapshot { get; set; }

        public FakeReader(HunterApprovedRewardSnapshot? snapshot)
        {
            Snapshot = snapshot;
        }

        public Task<HunterApprovedRewardSnapshot?>
            FindBySubmissionIdAsync(
                Guid submissionId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Snapshot);
        }
    }

    // In-memory test adapter only, NOT production SQL storage.
    private sealed class FakeRewardStore : ICoinRewardGrantStore
    {
        private readonly ConcurrentDictionary<
            string, CoinLedgerDraftAggregate> _wallets =
            new(StringComparer.Ordinal);

        private readonly ConcurrentBag<CoinRewardGrantRequest>
            _requests = new();

        public CoinRewardGrantRequest[] Requests =>
            _requests.ToArray();

        public CoinLedgerDraftAggregate? Wallet(string owner)
        {
            _wallets.TryGetValue(owner, out var wallet);
            return wallet;
        }

        public Task<bool> TryGrantOnceAsync(
            CoinRewardGrantRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CoinRewardContractRules.ValidateGrant(request);

            var wallet = _wallets.GetOrAdd(
                request.RecipientUserId,
                owner => new CoinLedgerDraftAggregate(
                    Guid.NewGuid(),
                    owner,
                    request.RuleVersion));

            var key = new CoinRewardUniqueKey(
                request.RecipientUserId,
                request.RewardType,
                request.SourceRecordId);

            bool credited = wallet.TryCreditReward(key);

            _requests.Add(request);

            return Task.FromResult(credited);
        }
    }
}
