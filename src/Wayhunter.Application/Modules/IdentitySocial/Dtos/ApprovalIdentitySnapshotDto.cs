
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Application.Modules.IdentitySocial.Dtos;

public sealed record ApprovalIdentitySnapshotDto(
    string UserId,
    string DisplayNameSnapshot,
    Guid ItemId,
    DateTimeOffset ServerTimestampUtc)
{
    public static ApprovalIdentitySnapshotDto Create(
        UserAccount user,
        Guid itemId,
        DateTimeOffset serverTimestampUtc)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (itemId == Guid.Empty)
        {
            throw new ArgumentException(
                "Item ID is required.");
        }

        if (serverTimestampUtc == default ||
            serverTimestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Server timestamp must be UTC.");
        }

        return new ApprovalIdentitySnapshotDto(
            user.UserId,
            user.DisplayName,
            itemId,
            serverTimestampUtc);
    }
}
