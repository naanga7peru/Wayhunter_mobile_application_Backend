using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var account = await _db.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalized,
            cancellationToken);
        if (account is null || account.Status != UserAccountStatus.PendingVerification)
        {
            await CommitAsync(transaction, cancellationToken);
            return new IdentityFlowResult(202);
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
            return new IdentityFlowResult(
                429,
                new { error = "otp_resend_cooldown" },
                cooldown.RetryAfter);
        }

        foreach (var oldChallenge in outstanding)
            oldChallenge.Revoke(now);

        var code = _codes.Generate();
        var challenge = CreateChallenge(account.UserId, code, now);
        _db.IdentityOtpChallenges.Add(challenge);
        await _db.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);

        if (!await SendOtpAsync(account, code, cancellationToken))
        {
            challenge.Revoke(_clock.GetUtcNow());
            await _db.SaveChangesAsync(cancellationToken);
            return new IdentityFlowResult(503, new { error = "otp_delivery_unavailable" });
        }

        return new IdentityFlowResult(202);
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
        var newlyActivated = false;
        UserAccount? accountForReward = null;

        await using (var transaction =
            await BeginSerializableAsync(cancellationToken))
        {
            var account = await _db.Users.SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalized,
                cancellationToken);
            if (account is null ||
                account.Status is UserAccountStatus.Suspended or
                    UserAccountStatus.DeletionRequested)
            {
                return InvalidOtp();
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
                    return InvalidOtp();
                accountForReward = account;
            }
            else
            {
                var challenge = challenges.FirstOrDefault(x =>
                    x.ConsumedAtUtc is null && x.RevokedAtUtc is null);
                if (challenge is null || !challenge.CanAttemptAt(now))
                    return InvalidOtp();

                if (!_verifier.Verify(
                        account.UserId, code, challenge.OtpVerifierHash))
                {
                    challenge.RecordFailedAttempt(now);
                    await _db.SaveChangesAsync(cancellationToken);
                    await CommitAsync(transaction, cancellationToken);
                    return InvalidOtp();
                }

                challenge.ConsumeAfterExternalVerification(now);
                account.ActivateAfterVerifiedOtp(now);
                await _db.SaveChangesAsync(cancellationToken);
                await CommitAsync(transaction, cancellationToken);
                newlyActivated = true;
                accountForReward = account;
            }
        }

        if (accountForReward is not null)
            await TryDeliverWelcomeRewardAsync(accountForReward, cancellationToken);

        return new IdentityFlowResult(204,
            new { verified = true, newlyActivated });
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

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>
        BeginSerializableAsync(CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
            return null;
        return await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
    }

    private static Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken) =>
        transaction is null
            ? Task.CompletedTask
            : transaction.CommitAsync(cancellationToken);

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320)
            return null;
        return email.Trim().ToLowerInvariant();
    }

    private static IdentityFlowResult InvalidOtp() =>
        new(400, new { error = "invalid_or_expired_otp" });
}
