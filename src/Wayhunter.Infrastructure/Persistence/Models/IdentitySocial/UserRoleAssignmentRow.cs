namespace Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;

/// <summary>
/// Trusted persistence record for an Administrator role assignment.
/// Schema design approved; SQL migration and activation pending.
/// </summary>
public sealed class UserRoleAssignmentRow
{
    public Guid AssignmentId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public string? AssignedByUserId { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? RevokedByUserId { get; private set; }

    // Required by EF Core.
    private UserRoleAssignmentRow()
    {
    }

    // Reserved for approved, trusted provisioning workflows.
    // No public API or production role provisioning is enabled.
    public static UserRoleAssignmentRow CreateTrusted(
        string userId,
        DateTimeOffset assignedAtUtc,
        string? assignedByUserId)
    {
        if (!Guid.TryParseExact(
                userId, "D", out var parsedUserId) ||
            parsedUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid UserId required.",
                nameof(userId));
        }

        if (assignedAtUtc == default ||
            assignedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "UTC assignment timestamp required.",
                nameof(assignedAtUtc));
        }

        if (assignedByUserId is not null &&
            (!Guid.TryParseExact(
                assignedByUserId,
                "D",
                out var parsedActor) ||
             parsedActor == Guid.Empty))
        {
            throw new ArgumentException(
                "Valid assignment actor required.",
                nameof(assignedByUserId));
        }

        return new UserRoleAssignmentRow
        {
            AssignmentId = Guid.NewGuid(),
            UserId = parsedUserId.ToString("D"),
            Role = "Admin",
            Status = "Active",
            AssignedAtUtc = assignedAtUtc,
            AssignedByUserId = assignedByUserId
        };
    }

    public void RevokeTrusted(
        DateTimeOffset revokedAtUtc,
        string? revokedByUserId)
    {
        if (Status != "Active")
        {
            throw new InvalidOperationException(
                "Only active assignments can be revoked.");
        }

        if (revokedAtUtc == default ||
            revokedAtUtc.Offset != TimeSpan.Zero ||
            revokedAtUtc < AssignedAtUtc)
        {
            throw new ArgumentException(
                "Invalid UTC revocation timestamp.",
                nameof(revokedAtUtc));
        }

        if (revokedByUserId is not null &&
            (!Guid.TryParseExact(
                revokedByUserId,
                "D",
                out var parsedActor) ||
             parsedActor == Guid.Empty))
        {
            throw new ArgumentException(
                "Valid revocation actor required.",
                nameof(revokedByUserId));
        }

        Status = "Revoked";
        RevokedAtUtc = revokedAtUtc;
        RevokedByUserId = revokedByUserId;
    }
}