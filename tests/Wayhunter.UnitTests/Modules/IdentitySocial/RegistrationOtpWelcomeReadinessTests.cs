
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class RegistrationOtpWelcomeReadinessTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount CreatePendingUser()
    {
        return UserAccount.RegisterPending(
            "Demo Traveler",
            "traveler@example.com",
            "test-only-password-hash",
            Now);
    }

    private static IdentityOtpChallenge CreateChallenge(
        UserAccount user,
        int maxAttempts = 5)
    {
        return IdentityOtpChallenge.Create(
            user.UserId,
            "test-only-verifier-hash",
            Now,
            Now.AddMinutes(5),
            maxAttempts);
    }

    [Fact]
    public async Task VerifiedAccount_RetryGrantsWelcomeOnce()
    {
        var user = CreatePendingUser();
        var challenge = CreateChallenge(user);
        var verifiedAt = Now.AddMinutes(2);

        // Simulates a successful trusted OTP verification.
        // Real code matching is not implemented here.
        Assert.True(challenge.CanAttemptAt(verifiedAt));

        challenge.ConsumeAfterExternalVerification(
            verifiedAt);

        user.ActivateAfterVerifiedOtp(verifiedAt);

        Assert.Equal(UserAccountStatus.Active, user.Status);
        Assert.Equal(verifiedAt, user.VerifiedAtUtc);

        var store = new FakeRewardStore();
        var handler = CreateHandler(user, store);

        Assert.True(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.False(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        var reward = Assert.Single(store.AcceptedRewards);

        Assert.Equal(user.UserId, reward.SourceRecordId);
        Assert.Equal(user.UserId, reward.RecipientUserId);
        Assert.Equal(100, reward.CoinsAmount);
    }

    [Fact]
    public async Task PendingAccount_CannotReceiveWelcomeReward()
    {
        var user = CreatePendingUser();
        var store = new FakeRewardStore();
        var handler = CreateHandler(user, store);

        Assert.False(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.Empty(store.AcceptedRewards);
    }

    [Fact]
    public async Task ExpiredOtp_DoesNotQualifyForActivation()
    {
        var user = CreatePendingUser();
        var challenge = CreateChallenge(user);

        Assert.False(challenge.CanAttemptAt(
            Now.AddMinutes(5)));

        Assert.Throws<InvalidOperationException>(() =>
            challenge.ConsumeAfterExternalVerification(
                Now.AddMinutes(5)));

        var store = new FakeRewardStore();
        var handler = CreateHandler(user, store);

        Assert.False(await handler.HandleAsync(
            user.UserId, Guid.NewGuid()));

        Assert.Equal(
            UserAccountStatus.PendingVerification,
            user.Status);
    }

    [Fact]
    public void TooManyWrongAttempts_BlockOtpReuse()
    {
        var user = CreatePendingUser();
        var challenge = CreateChallenge(
            user,
            maxAttempts: 3);

        challenge.RecordFailedAttempt(Now.AddSeconds(10));
        challenge.RecordFailedAttempt(Now.AddSeconds(20));
        challenge.RecordFailedAttempt(Now.AddSeconds(30));

        Assert.Equal(3, challenge.FailedAttemptCount);

        Assert.False(challenge.CanAttemptAt(
            Now.AddSeconds(40)));

        Assert.Throws<InvalidOperationException>(() =>
            challenge.ConsumeAfterExternalVerification(
                Now.AddSeconds(40)));
    }

    private static GrantVerifiedWelcomeCoinsHandler
        CreateHandler(
            UserAccount account,
            FakeRewardStore store)
    {
        return new GrantVerifiedWelcomeCoinsHandler(
            new FakeAccountReader(account),
            store,
            "2026-11-11");
    }

    private sealed class FakeAccountReader
        : IVerifiedWelcomeAccountReader
    {
        private readonly UserAccount _account;

        public FakeAccountReader(UserAccount account)
        {
            _account = account;
        }

        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UserAccount? result =
                _account.UserId == userId
                    ? _account
                    : null;

            return Task.FromResult(result);
        }
    }

    // Test-only store, not production persistence.
    private sealed class FakeRewardStore
        : ICoinRewardGrantStore
    {
        private readonly HashSet<string> _uniqueKeys =
            new(StringComparer.Ordinal);

        public List<CoinRewardGrantRequest> AcceptedRewards
        { get; } = new();

        public Task<bool> TryGrantOnceAsync(
            CoinRewardGrantRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CoinRewardContractRules.ValidateGrant(request);

            var key =
                $"{request.RecipientUserId}:" +
                CoinRewardContractRules.BuildIdempotencyKey(
                    request.RewardType,
                    request.SourceRecordId);

            if (!_uniqueKeys.Add(key))
            {
                return Task.FromResult(false);
            }

            AcceptedRewards.Add(request);

            return Task.FromResult(true);
        }
    }
}
