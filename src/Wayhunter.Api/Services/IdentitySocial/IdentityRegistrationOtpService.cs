using System.Data;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Application.Modules.IdentitySocial.Services;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Events;
using Wayhunter.Infrastructure.Integrations.Email;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed record IdentityFlowResult(
    int StatusCode,
    object? Response = null,
    TimeSpan? RetryAfter = null);

public sealed class IdentityRegistrationOtpService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaxOtpAttempts = 5;
    private const int OtpDisplayExpiryMinutes = 5;

    private readonly WayhunterSharedDbContext _db;
    private readonly RegistrationPreparationService _registration;
    private readonly IdentityOtpVerifier _verifier;
    private readonly IIdentityOtpCodeGenerator _codes;
    private readonly IIdentityOtpEmailSender _emailSender;
    private readonly TimeProvider _clock;
    private readonly IServiceProvider _services;
    private readonly ILogger<IdentityRegistrationOtpService> _logger;

    public IdentityRegistrationOtpService(
        WayhunterSharedDbContext db,
        RegistrationPreparationService registration,
        IdentityOtpVerifier verifier,
        IIdentityOtpCodeGenerator codes,
        IIdentityOtpEmailSender emailSender,
        TimeProvider clock,
        IServiceProvider services,
        ILogger<IdentityRegistrationOtpService> logger)
    {
        _db = db;
        _registration = registration;
        _verifier = verifier;
        _codes = codes;
        _emailSender = emailSender;
        _clock = clock;
        _services = services;
        _logger = logger;
    }

    public async Task<IdentityFlowResult> RegisterAsync(
        string? displayName,
        string? email,
        string? password,
        string? confirmPassword,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        PreparedRegistration prepared;
        try
        {
            prepared = _registration.Prepare(
                displayName ?? string.Empty,
                email ?? string.Empty,
                password ?? string.Empty,
                confirmPassword ?? string.Empty,
                now);
        }
        catch (ArgumentException)
        {
            return new IdentityFlowResult(400, new { error = "invalid_registration" });
        }

        var account = prepared.Account;
        var duplicate = await _db.Users.AnyAsync(
            x => x.NormalizedEmail == account.NormalizedEmail,
            cancellationToken);
        if (duplicate)
            return new IdentityFlowResult(409, new { error = "account_conflict" });

        var code = _codes.Generate();
        var challenge = CreateChallenge(account.UserId, code, now);
        _db.Users.Add(account);
        _db.UserProfiles.Add(UserProfile.CreateForUser(account, now));
        _db.IdentityOtpChallenges.Add(challenge);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return new IdentityFlowResult(409, new { error = "account_conflict" });
        }

        var sent = await SendOtpAsync(account, code, cancellationToken);
        if (!sent)
        {
            challenge.Revoke(now);
            await _db.SaveChangesAsync(cancellationToken);
            return new IdentityFlowResult(503, new { error = "otp_delivery_unavailable" });
        }

        return new IdentityFlowResult(201, prepared.Response);
    }

    public async Task<IdentityFlowResult> ResendAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null)
            return new IdentityFlowResult(202);

        var now = _clock.GetUtcNow();
        var code = _codes.Generate();
        var outcome = await ExecuteSerializableAsync(async () =>
        {
            var account = await _db.Users.SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalized,
                cancellationToken);
            if (account is null || account.Status != UserAccountStatus.PendingVerification)
                return new ResendTransactionResult(new IdentityFlowResult(202));

            var verifierHash = _verifier.Hash(account.UserId, code);
            var retriedChallenge = await _db.IdentityOtpChallenges
                .SingleOrDefaultAsync(x =>
                    x.UserId == account.UserId &&
                    x.CreatedAtUtc == now &&
                    x.OtpVerifierHash == verifierHash &&
                    x.ConsumedAtUtc == null &&
                    x.RevokedAtUtc == null,
                    cancellationToken);
            if (retriedChallenge is not null)
            {
                return new ResendTransactionResult(
                    new IdentityFlowResult(202), account, retriedChallenge, code);
            }

            var outstanding = await _db.IdentityOtpChallenges
                .Where(x => x.UserId == account.UserId &&
                            x.ConsumedAtUtc == null &&
                            x.RevokedAtUtc == null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var cooldown = OtpResendCooldownPolicy.Check(
                now,
                outstanding.FirstOrDefault()?.CreatedAtUtc,
                ResendCooldown);
            if (!cooldown.Allowed)
            {
                return new ResendTransactionResult(new IdentityFlowResult(
                    429,
                    new { error = "otp_resend_cooldown" },
                    cooldown.RetryAfter));
            }

            foreach (var oldChallenge in outstanding)
                oldChallenge.Revoke(now);

            var challenge = CreateChallenge(account.UserId, code, now);
            _db.IdentityOtpChallenges.Add(challenge);
            await _db.SaveChangesAsync(cancellationToken);
            return new ResendTransactionResult(
                new IdentityFlowResult(202), account, challenge, code);
        }, cancellationToken);

        if (outcome.Account is null || outcome.Challenge is null || outcome.Code is null)
            return outcome.Result;

        if (!await SendOtpAsync(outcome.Account, outcome.Code, cancellationToken))
        {
            outcome.Challenge.Revoke(_clock.GetUtcNow());
            await _db.SaveChangesAsync(cancellationToken);
            return new IdentityFlowResult(503, new { error = "otp_delivery_unavailable" });
        }

        return outcome.Result;
    }

    public async Task<IdentityFlowResult> VerifyAsync(
        string? email,
        string? code,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null || string.IsNullOrWhiteSpace(code))
            return InvalidOtp();

        var now = _clock.GetUtcNow();
        var outcome = await ExecuteSerializableAsync(async () =>
        {
            var account = await _db.Users.SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalized,
                cancellationToken);
            if (account is null ||
                account.Status is UserAccountStatus.Suspended or
                    UserAccountStatus.DeletionRequested)
            {
                return new VerifyTransactionResult(InvalidOtp(), null);
            }

            var challenges = await _db.IdentityOtpChallenges
                .Where(x => x.UserId == account.UserId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            if (account.Status == UserAccountStatus.Active)
            {
                var consumed = challenges.FirstOrDefault(x =>
                    x.ConsumedAtUtc is not null &&
                    _verifier.Verify(account.UserId, code, x.OtpVerifierHash));
                if (consumed is null)
                    return new VerifyTransactionResult(InvalidOtp(), null);
                return new VerifyTransactionResult(
                    new IdentityFlowResult(204,
                        new { verified = true, newlyActivated = false }), account);
            }
            else
            {
                var challenge = challenges.FirstOrDefault(x =>
                    x.ConsumedAtUtc is null && x.RevokedAtUtc is null);
                if (challenge is null || !challenge.CanAttemptAt(now))
                    return new VerifyTransactionResult(InvalidOtp(), null);

                if (!_verifier.Verify(
                        account.UserId, code, challenge.OtpVerifierHash))
                {
                    challenge.RecordFailedAttempt(now);
                    await _db.SaveChangesAsync(cancellationToken);
                    return new VerifyTransactionResult(InvalidOtp(), null);
                }

                challenge.ConsumeAfterExternalVerification(now);
                account.ActivateAfterVerifiedOtp(now);
                await _db.SaveChangesAsync(cancellationToken);
                return new VerifyTransactionResult(
                    new IdentityFlowResult(204,
                        new { verified = true, newlyActivated = true }), account);
            }
        }, cancellationToken);

        if (outcome.AccountForReward is not null)
            await TryDeliverWelcomeRewardAsync(outcome.AccountForReward, cancellationToken);

        return outcome.Result;
    }

    private IdentityOtpChallenge CreateChallenge(
        string userId,
        string code,
        DateTimeOffset now) =>
        IdentityOtpChallenge.Create(
            userId,
            _verifier.Hash(userId, code),
            now,
            now.Add(OtpLifetime),
            MaxOtpAttempts);

    private async Task<bool> SendOtpAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken)
    {
        var content = OtpEmailTemplate.Create(
            account.DisplayName, code, OtpDisplayExpiryMinutes);
        return await _emailSender.SendAsync(
            account.NormalizedEmail, content, cancellationToken);
    }

    private async Task TryDeliverWelcomeRewardAsync(
        UserAccount account,
        CancellationToken cancellationToken)
    {
        var handler = _services.GetService<HandleVerifiedAccountActivatedReward>();
        if (handler is null)
            return;

        var activation = VerifiedAccountActivatedEvent
            .CreateFromVerifiedAccount(account, Guid.NewGuid());
        try
        {
            await handler.HandleAsync(activation, cancellationToken);
        }
        catch
        {
            // Account activation is already committed. Keep the response safe;
            // durable retry requires the missing M1 activation outbox table.
            _logger.LogError(
                "Welcome reward delivery failed after activation. ActivationEventId={ActivationEventId}",
                activation.EventId);
        }
    }

    private async Task<T> ExecuteSerializableAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attempt++ > 0)
                _db.ChangeTracker.Clear();

            if (!_db.Database.IsRelational())
                return await operation();

            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private sealed record ResendTransactionResult(
        IdentityFlowResult Result,
        UserAccount? Account = null,
        IdentityOtpChallenge? Challenge = null,
        string? Code = null);

    private sealed record VerifyTransactionResult(
        IdentityFlowResult Result,
        UserAccount? AccountForReward);

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320)
            return null;
        return email.Trim().ToLowerInvariant();
    }

    private static IdentityFlowResult InvalidOtp() =>
        new(400, new { error = "invalid_or_expired_otp" });
}
