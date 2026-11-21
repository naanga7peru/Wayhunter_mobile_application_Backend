using System.Security.Cryptography;
using System.Text.Json;

namespace Wayhunter.Application.Modules.TripSafety.Notifications;

public static class NotificationTemplates
{
    public static NotificationContent Get(NotificationTemplate template) => template switch
    {
        NotificationTemplate.TripReminder => Make(NotificationCategory.Trip, template, "Trip reminder"),
        NotificationTemplate.SafetyUpdate => Make(NotificationCategory.Safety, template, "Safety update"),
        NotificationTemplate.SocialUpdate => Make(NotificationCategory.Social, template, "Social update"),
        NotificationTemplate.HunterReview => Make(NotificationCategory.Hunter, template, "Hunter review update"),
        NotificationTemplate.GameReview => Make(NotificationCategory.Game, template, "Game review update"),
        NotificationTemplate.CoinsUpdate => Make(NotificationCategory.Coins, template, "Coins update"),
        NotificationTemplate.VoucherUpdate => Make(NotificationCategory.Voucher, template, "Voucher update"),
        _ => throw new ArgumentException("Unsupported notification template.")
    };

    private static NotificationContent Make(NotificationCategory category, NotificationTemplate template, string title) =>
        new(category, template, 1, title, "Open Wayhunter to view the update.");

    public static void ValidateTarget(NotificationContent template, NotificationTarget? target)
    {
        if (target is not null && (target.Id == Guid.Empty || target.Category != template.Category))
            throw new ArgumentException("Unsupported notification target.");
    }
}

public sealed class NotificationComposerService(INotificationAudienceDirectory directory, INotificationJobs jobs,
    TimeProvider clock)
{
    public async Task<NotificationAudiencePreview> PreviewAsync(string actorId, NotificationAdminCommand command,
        CancellationToken ct)
    {
        if (command.SendAtUtc is { } due && due <= clock.GetUtcNow())
            throw new ArgumentException("Preview requires a future schedule.");

        var (recipients, fingerprint) = await PrepareAsync(actorId, command, ct);
        var receipt = new NotificationAudienceReceipt(Guid.NewGuid(), actorId, fingerprint, recipients.Length,
            clock.GetUtcNow().AddMinutes(5));
        await jobs.SavePreviewAsync(receipt, ct);
        return new(receipt.ReceiptId, receipt.Count, receipt.ExpiresAtUtc);
    }

    public async Task<NotificationJobReceipt> ScheduleAsync(string actorId, NotificationAdminCommand command,
        bool confirmBulk, Guid? previewReceiptId, CancellationToken ct)
    {
        var (recipients, fingerprint) = await PrepareAsync(actorId, command, ct);
        if (recipients.Length > 1 && (!confirmBulk || previewReceiptId is null || previewReceiptId == Guid.Empty))
            throw new NotificationConflictException();

        return await jobs.ScheduleAdminAsync(new(actorId, command, recipients, fingerprint,
            recipients.Length > 1 ? previewReceiptId : null, clock.GetUtcNow()), ct);
    }

    public async Task<bool> CancelAsync(string actorId, Guid jobId, int revision, Guid operationId, CancellationToken ct)
    {
        NotificationInputs.CheckOperation(jobId, operationId, revision);
        if (!await directory.CanManageAsync(actorId, ct))
            throw new UnauthorizedAccessException();

        return await jobs.CancelAsync(NotificationJobKind.AdminMessage, actorId, jobId, revision,
            operationId, clock.GetUtcNow(), ct);
    }

    public async Task<NotificationDeliverySummary?> SummaryAsync(string actorId, Guid jobId, CancellationToken ct)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("Invalid notification job.");
        if (!await directory.CanManageAsync(actorId, ct))
            throw new UnauthorizedAccessException();
        return await jobs.ReadSummaryAsync(actorId, jobId, ct);
    }

    private async Task<(string[] Recipients, string Fingerprint)> PrepareAsync(string actorId,
        NotificationAdminCommand command, CancellationToken ct)
    {
        if (!NotificationInputs.IsCanonicalUser(actorId))
            throw new UnauthorizedAccessException();
        if (command.OperationId == Guid.Empty || command.Audience is null ||
            command.Text is not null || command.DeepLink is not null ||
            (command.SendAtUtc is { } sendAt && sendAt.Offset != TimeSpan.Zero))
            throw new ArgumentException("Use a supported template, typed target and future UTC schedule.");

        var template = NotificationTemplates.Get(command.Template);
        NotificationTemplates.ValidateTarget(template, command.Target);
        var filter = command.Audience;
        var requested = filter.UserIds;
        var district = filter.District;
        if ((district is not null && (district.Length is < 1 or > 40 ||
                !district.All(c => char.IsAsciiLetter(c) || c is ' ' or '-'))) ||
            (filter.Role is { } role && !Enum.IsDefined(role)) ||
            (requested is not null && (requested.Length is < 1 or > 100 ||
                requested.Any(id => !NotificationInputs.IsCanonicalUser(id)))) ||
            (requested is null && filter.Role is null && district is null))
            throw new ArgumentException("A bounded approved recipient scope is required.");

        if (!await directory.CanManageAsync(actorId, ct))
            throw new UnauthorizedAccessException();
        var members = await directory.ResolveAsync(actorId, filter, template.Category, command.Target, ct);
        if (members.Any(x => !NotificationInputs.IsCanonicalUser(x.UserId) ||
                (filter.Role is not null && x.Role != filter.Role) ||
                (district is not null && !string.Equals(x.District, district, StringComparison.OrdinalIgnoreCase)) ||
                (requested is not null && !requested.Contains(x.UserId, StringComparer.Ordinal))))
            throw new UnauthorizedAccessException();

        var recipients = members.Select(x => x.UserId).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if (recipients.Length is < 1 or > 100)
            throw new NotificationConflictException();

        var binding = JsonSerializer.SerializeToUtf8Bytes(new
        {
            actorId, command.OperationId, command.Template, template.Version, command.Target, command.SendAtUtc,
            filter.Role, District = district?.ToUpperInvariant(),
            Requested = requested?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), recipients
        });
        return (recipients, Convert.ToHexString(SHA256.HashData(binding)));
    }
}

public static class NotificationInputs
{
    public static bool IsCanonicalUser(string? id) =>
        Guid.TryParseExact(id, "D", out var parsed) && parsed != Guid.Empty && parsed.ToString("D") == id;

    public static void CheckOperation(Guid resourceId, Guid operationId, int revision = 1)
    {
        if (resourceId == Guid.Empty || operationId == Guid.Empty || revision < 1)
            throw new ArgumentException("Invalid notification operation.");
    }

    public static void ValidateDevice(Guid deviceId, Guid operationId, string? token)
    {
        CheckOperation(deviceId, operationId);
        if (token is null || token.Length is < 20 or > 4096 ||
            !token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.'))
            throw new ArgumentException("Invalid device registration.");
    }
}
