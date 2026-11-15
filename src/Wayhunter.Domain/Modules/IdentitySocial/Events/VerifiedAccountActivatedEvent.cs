
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Domain.Modules.IdentitySocial.Events;

public sealed record VerifiedAccountActivatedEvent
{
    public Guid EventId { get; }
    public string UserId { get; }
    public DateTimeOffset VerifiedAtUtc { get; }
    public Guid CorrelationId { get; }

    private VerifiedAccountActivatedEvent(
        Guid eventId,
        string userId,
        DateTimeOffset verifiedAtUtc,
        Guid correlationId)
    {
        EventId = eventId;
        UserId = userId;
        VerifiedAtUtc = verifiedAtUtc;
        CorrelationId = correlationId;
    }

    public static VerifiedAccountActivatedEvent
        CreateFromVerifiedAccount(
            UserAccount account,
            Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));
        }

        if (account.Status != UserAccountStatus.Active ||
            account.VerifiedAtUtc is null)
        {
            throw new InvalidOperationException(
                "Account must be verified and active.");
        }

        var verifiedAt = account.VerifiedAtUtc.Value;

        if (verifiedAt == default ||
            verifiedAt.Offset != TimeSpan.Zero ||
            verifiedAt < account.CreatedAtUtc)
        {
            throw new InvalidOperationException(
                "Invalid verification timestamp.");
        }

        if (string.IsNullOrWhiteSpace(account.UserId))
        {
            throw new InvalidOperationException(
                "Verified account requires a User ID.");
        }

        return new VerifiedAccountActivatedEvent(
            Guid.NewGuid(),
            account.UserId,
            verifiedAt,
            correlationId);
    }
}
