using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class PasswordRecoveryService
{
    private readonly WayhunterSharedDbContext _db;
    private readonly RegistrationPreparationService _passwords;
    private readonly IPasswordResetEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly ILogger<PasswordRecoveryService> _logger;

    public PasswordRecoveryService(
        WayhunterSharedDbContext db,
        RegistrationPreparationService passwords,
        IPasswordResetEmailSender emailSender,
        IConfiguration configuration,
        TimeProvider clock,
        ILogger<PasswordRecoveryService> logger)
    {
        _db = db;
        _passwords = passwords;
        _emailSender = emailSender;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    // Always completes without disclosing whether the account exists.
    public async Task RequestResetAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var lifetime = _configuration.GetValue<int?>(
            "Identity:PasswordResetLifetimeMinutes");
        if (normalizedEmail is null ||
            lifetime is null || lifetime is < 1 or > 1440)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        var token = CreateToken();
        var tokenHash = HashToken(token);
        var issuance = await ExecuteSerializableAsync(async () =>
        {
            var account = await _db.Users.SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken);
            if (account is null ||
                account.Status != UserAccountStatus.Active ||
                !account.VerifiedAtUtc.HasValue ||
                account.DeletionRequestedAtUtc.HasValue)
            {
                return null;
            }

            // A retry after an ambiguous commit finds the same credential and
            // can safely proceed to the single email delivery below.
            var committed = await _db.IdentityPasswordResetCredentials
                .SingleOrDefaultAsync(
                    x => x.UserId == account.UserId && x.TokenHash == tokenHash,
                    cancellationToken);
            if (committed is not null)
                return new ResetIssuance(committed, account.DisplayName);

            var credential = IdentityPasswordResetCredential.Create(
                account.UserId,
                tokenHash,
                now,
                now.AddMinutes(lifetime.Value));
            var outstanding = await _db.IdentityPasswordResetCredentials
                .Where(x => x.UserId == account.UserId &&
                            x.ConsumedAtUtc == null &&
                            x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var existing in outstanding)
                existing.RevokeAt(now);

            _db.IdentityPasswordResetCredentials.Add(credential);
            await _db.SaveChangesAsync(cancellationToken);
            return new ResetIssuance(credential, account.DisplayName);
        }, cancellationToken);

        if (issuance is null)
            return;

        try
        {
            var delivered = await _emailSender.SendAsync(
                normalizedEmail,
                issuance.DisplayName,
                token,
                lifetime.Value,
                cancellationToken);
            if (!delivered)
                await RevokeUndeliveredAsync(issuance.Credential, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await RevokeUndeliveredAsync(issuance.Credential, CancellationToken.None);
            throw;
        }
        catch
        {
            await RevokeUndeliveredAsync(issuance.Credential, CancellationToken.None);
            _logger.LogWarning("Password reset email delivery failed.");
        }
    }

    public async Task<bool> ResetPasswordAsync(
        string? email,
        string? token,
        string? newPassword,
        string? confirmPassword,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null || string.IsNullOrWhiteSpace(token))
            return false;

        string passwordHash;
        try
        {
            passwordHash = _passwords.HashPassword(
                newPassword ?? string.Empty,
                confirmPassword ?? string.Empty);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var now = _clock.GetUtcNow();
        var tokenHash = HashToken(token);
        return await ExecuteSerializableAsync(async () =>
        {
            var account = await _db.Users.SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken);
            if (account is null ||
                account.Status != UserAccountStatus.Active ||
                !account.VerifiedAtUtc.HasValue ||
                account.DeletionRequestedAtUtc.HasValue)
            {
                return false;
            }

            var credential = await _db.IdentityPasswordResetCredentials
                .SingleOrDefaultAsync(
                    x => x.UserId == account.UserId && x.TokenHash == tokenHash,
                    cancellationToken);
            if (credential is null)
                return false;

            // If SQL committed but the connection failed during commit, the
            // execution strategy retries. Recognize this same operation.
            if (credential.ConsumedAtUtc.HasValue)
                return string.Equals(
                    account.PasswordHash, passwordHash, StringComparison.Ordinal);
            if (!credential.CanUseAt(now))
                return false;

            credential.ConsumeAt(now);
            account.ChangePasswordHash(passwordHash);
            var activeSessions = await _db.IdentitySessions
                .Where(x => x.UserId == account.UserId &&
                            x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var session in activeSessions)
                session.Revoke(now);

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task RevokeUndeliveredAsync(
        IdentityPasswordResetCredential credential,
        CancellationToken cancellationToken)
    {
        if (credential.RevokedAtUtc is not null ||
            credential.ConsumedAtUtc is not null)
            return;
        credential.RevokeAt(_clock.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);
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

    private sealed record ResetIssuance(
        IdentityPasswordResetCredential Credential,
        string DisplayName);

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320)
            return null;
        var trimmed = email.Trim();
        if (!MailAddress.TryCreate(trimmed, out var parsed) ||
            !string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
            return null;
        return trimmed.ToLowerInvariant();
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
