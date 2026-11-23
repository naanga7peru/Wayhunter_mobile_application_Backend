using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Contracts;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class BlockedUserRelationshipService(WayhunterSharedDbContext db, TimeProvider clock)
{
    public async Task BlockAsync(string actorUserId, string targetUserId, CancellationToken ct)
    {
        if (actorUserId == targetUserId)
            throw new InvalidOperationException("Users cannot block themselves.");

        await EnsureActiveActorAsync(actorUserId, ct);
        await EnsureTargetExistsAsync(targetUserId, ct);

        if (await db.BlockedUserRelationships.AnyAsync(
                x => x.BlockerUserId == actorUserId && x.BlockedUserId == targetUserId, ct))
            return;

        db.BlockedUserRelationships.Add(new BlockedUserRelationship(
            actorUserId, targetUserId, clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent identical block is an idempotent success. Other SQL
            // failures remain visible and cannot be mistaken for a stored block.
            db.Entry(db.ChangeTracker.Entries<BlockedUserRelationship>().Single().Entity).State = EntityState.Detached;
            if (!await db.BlockedUserRelationships.AsNoTracking().AnyAsync(
                    x => x.BlockerUserId == actorUserId && x.BlockedUserId == targetUserId, ct))
                throw;
        }
    }

    public async Task UnblockAsync(string actorUserId, string targetUserId, CancellationToken ct)
    {
        if (actorUserId == targetUserId)
            throw new InvalidOperationException("Users cannot unblock themselves.");

        await EnsureActiveActorAsync(actorUserId, ct);
        await EnsureTargetExistsAsync(targetUserId, ct);
        await db.BlockedUserRelationships
            .Where(x => x.BlockerUserId == actorUserId && x.BlockedUserId == targetUserId)
            .ExecuteDeleteAsync(ct);
    }

    private async Task EnsureActiveActorAsync(string userId, CancellationToken ct)
    {
        var account = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account))
            throw new UnauthorizedAccessException("An active verified user account is required.");
    }

    private async Task EnsureTargetExistsAsync(string userId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.UserId == userId, ct))
            throw new KeyNotFoundException("User was not found.");
    }
}
