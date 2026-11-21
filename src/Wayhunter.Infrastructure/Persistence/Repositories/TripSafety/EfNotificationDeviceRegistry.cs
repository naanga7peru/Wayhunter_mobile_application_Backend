using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class ProtectedNotificationToken(string fingerprint, byte[] ciphertext)
{
    public string Fingerprint { get; } = fingerprint;
    public byte[] Ciphertext { get; } = ciphertext;
}

public interface INotificationTokenProtector
{
    // Requires approved installation/token ownership verification, keyed fingerprint
    // (64 uppercase hex characters), and secret-backed reversible encryption.
    Task<ProtectedNotificationToken> VerifyAndProtectAsync(string ownerId, Guid deviceId, string rawToken, CancellationToken ct);
}

// Intentionally unregistered until token ownership proof and secret storage are approved.
public sealed class EfNotificationDeviceRegistry(IDbContextFactory<WayhunterSharedDbContext> contexts,
    INotificationTokenProtector protector, TimeProvider clock) : INotificationDeviceRegistry
{
    public async Task<NotificationDeviceReceipt> RegisterOrRotateAsync(string ownerId, Guid deviceId, Guid operationId,
        string rawToken, bool pushAllowed, CancellationToken ct)
    {
        VerifyOwnerId(ownerId);
        NotificationInputs.ValidateDevice(deviceId, operationId, rawToken);
        var token = await protector.VerifyAndProtectAsync(ownerId, deviceId, rawToken, ct);
        if (token.Fingerprint.Length != 64 || !token.Fingerprint.All(char.IsAsciiHexDigitUpper) ||
            token.Ciphertext.Length is < 1 or > 8192)
            throw new NotificationUnavailableException();

        var fingerprint = Hash($"register:{deviceId:D}:{token.Fingerprint}:{pushAllowed}");
        return await WriteAsync(ownerId, async db =>
        {
            var old = await OperationAsync(db, ownerId, operationId, "RegisterDevice", fingerprint, ct);
            if (old is not null)
                return new NotificationDeviceReceipt(deviceId, operationId, old.RecordedAtUtc);

            var device = await db.NotificationDevices.FindAsync([deviceId], ct);
            if (device is not null && device.UserId != ownerId)
                throw new UnauthorizedAccessException();

            var binding = await db.NotificationTokenBindings.FindAsync([token.Fingerprint], ct);
            if (binding is not null && (binding.DeviceId != deviceId || binding.UserId != ownerId))
                throw new NotificationConflictException();

            var now = clock.GetUtcNow();
            if (device is null)
            {
                device = new NotificationDeviceRow { DeviceId = deviceId, UserId = ownerId };
                db.NotificationDevices.Add(device);
            }
            if (binding is null)
                db.NotificationTokenBindings.Add(new NotificationTokenBindingRow
                {
                    Fingerprint = token.Fingerprint, DeviceId = deviceId, UserId = ownerId, RecordedAtUtc = now
                });
            device.TokenFingerprint = token.Fingerprint;
            device.ProtectedToken = pushAllowed ? token.Ciphertext : [];
            device.PushAllowed = pushAllowed;
            device.ChangedAtUtc = now;
            device.RevokedAtUtc = null;
            AddOperation(db, ownerId, deviceId, operationId, "RegisterDevice", fingerprint, now);
            return new NotificationDeviceReceipt(deviceId, operationId, now);
        }, ct);
    }

    public Task<bool> RevokeAsync(string ownerId, Guid deviceId, Guid operationId, CancellationToken ct)
    {
        VerifyOwnerId(ownerId);
        NotificationInputs.CheckOperation(deviceId, operationId);
        var fingerprint = Hash($"revoke:{deviceId:D}");
        return WriteAsync(ownerId, async db =>
        {
            if (await OperationAsync(db, ownerId, operationId, "RevokeDevice", fingerprint, ct) is not null)
                return true;
            var device = await db.NotificationDevices.SingleOrDefaultAsync(x => x.DeviceId == deviceId &&
                x.UserId == ownerId, ct);
            if (device is null)
                return false;

            var now = clock.GetUtcNow();
            if (device.RevokedAtUtc is null)
            {
                device.RevokedAtUtc = now;
                device.ChangedAtUtc = now;
                device.PushAllowed = false;
                device.ProtectedToken = [];
            }
            AddOperation(db, ownerId, deviceId, operationId, "RevokeDevice", fingerprint, now);
            return true;
        }, ct);
    }

    // Approved transports can call this for a provider-confirmed invalid device version.
    // A rotated token or concurrent update fences the stale response.
    public Task<bool> RevokeInvalidVersionAsync(string ownerId, Guid deviceId, byte[] rowVersion, CancellationToken ct)
    {
        VerifyOwnerId(ownerId);
        if (deviceId == Guid.Empty || rowVersion.Length != 8)
            throw new ArgumentException("Invalid device version.");
        return WriteAsync(ownerId, async db =>
        {
            var now = clock.GetUtcNow();
            return await db.NotificationDevices.Where(x => x.UserId == ownerId && x.DeviceId == deviceId &&
                    x.RevokedAtUtc == null && x.RowVersion == rowVersion)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.ChangedAtUtc, now)
                    .SetProperty(x => x.PushAllowed, false)
                    .SetProperty(x => x.ProtectedToken, Array.Empty<byte>()), ct) == 1;
        }, ct);
    }

    private static void VerifyOwnerId(string id)
    {
        if (!NotificationInputs.IsCanonicalUser(id))
            throw new UnauthorizedAccessException();
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task<NotificationOperationRow?> OperationAsync(WayhunterSharedDbContext db, string ownerId,
        Guid operationId, string action, string fingerprint, CancellationToken ct)
    {
        var record = await db.NotificationOperations.FindAsync([ownerId, operationId], ct);
        if (record is not null && (record.Action != action || record.Fingerprint != fingerprint))
            throw new NotificationConflictException();
        return record;
    }

    private static void AddOperation(WayhunterSharedDbContext db, string userId, Guid deviceId, Guid operationId,
        string action, string fingerprint, DateTimeOffset now) => db.NotificationOperations.Add(new NotificationOperationRow
        {
            ActorId = userId, OperationId = operationId, ResourceId = deviceId,
            Action = action, Fingerprint = fingerprint, RecordedAtUtc = now
        });

    private async Task<T> WriteAsync<T>(string ownerId, Func<WayhunterSharedDbContext, Task<T>> action, CancellationToken ct)
    {
        await using var strategy = await contexts.CreateDbContextAsync(ct);
        if (!strategy.Database.IsSqlServer())
            throw new NotificationUnavailableException();

        return await strategy.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var db = await contexts.CreateDbContextAsync(token);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            if (!await db.Users.AnyAsync(x => x.UserId == ownerId && x.Status == UserAccountStatus.Active &&
                x.VerifiedAtUtc != null && x.DeletionRequestedAtUtc == null, token))
                throw new UnauthorizedAccessException();

            var result = await action(db);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return result;
        }, ct);
    }
}
