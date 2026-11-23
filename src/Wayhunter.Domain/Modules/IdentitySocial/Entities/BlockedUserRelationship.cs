namespace Wayhunter.Domain.Modules.IdentitySocial.Entities;

/// <summary>A directional block created by its owning user.</summary>
public sealed class BlockedUserRelationship
{
    public string BlockerUserId { get; private set; } = null!;
    public string BlockedUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private BlockedUserRelationship() { }

    public BlockedUserRelationship(
        string blockerUserId,
        string blockedUserId,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(blockerUserId))
            throw new ArgumentException("Blocker user ID is required.", nameof(blockerUserId));
        if (string.IsNullOrWhiteSpace(blockedUserId))
            throw new ArgumentException("Blocked user ID is required.", nameof(blockedUserId));
        if (string.Equals(blockerUserId, blockedUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Users cannot block themselves.");
        if (createdAtUtc == default)
            throw new ArgumentException("Block timestamp is required.", nameof(createdAtUtc));

        BlockerUserId = blockerUserId;
        BlockedUserId = blockedUserId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }
}
