using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        IdentityPasswordResetCredential credential;
        string displayName;

        await using (var transaction = await BeginSerializableAsync(cancellationToken))
        {
            var account = await _db.Users.SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken);
            if (account is null ||
                account.Status != UserAccountStatus.Active ||
                !account.VerifiedAtUtc.HasValue ||
                account.DeletionRequestedAtUtc.HasValue)
            {
                return;
            }

            displayName = account.DisplayName;
            credential = IdentityPasswordResetCredential.Create(
                account.UserId,
                HashToken(token),
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
            await CommitAsync(transaction, cancellationToken);
        }

        try
        {
            var delivered = await _emailSender.SendAsync(
                normalizedEmail,
                displayName,
                token,
                lifetime.Value,
                cancellationToken);
            if (!delivered)
                await RevokeUndeliveredAsync(credential, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await RevokeUndeliveredAsync(credential, CancellationToken.None);
            throw;
        }
        catch
        {
            await RevokeUndeliveredAsync(credential, CancellationToken.None);
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
        await using var transaction = await BeginSerializableAsync(cancellationToken);
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

        var tokenHash = HashToken(token);
        var credential = await _db.IdentityPasswordResetCredentials
            .SingleOrDefaultAsync(
                x => x.UserId == account.UserId && x.TokenHash == tokenHash,
                cancellationToken);
        if (credential is null || !credential.CanUseAt(now))
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
        await CommitAsync(transaction, cancellationToken);
        return true;
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

    private async Task<IDbContextTransaction?> BeginSerializableAsync(
        CancellationToken cancellationToken) =>
        _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken)
            : null;

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
