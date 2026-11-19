using Microsoft.EntityFrameworkCore;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

/// <summary>
/// Trusted, read-only M1 Administrator assignment lookup.
/// Not registered in production dependency injection.
/// </summary>
public sealed class SqlTrustedAdministratorRoleReader
    : ITrustedAdministratorRoleReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlTrustedAdministratorRoleReader(
        WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AdministratorRoleAssignment?>
        FindAssignmentAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Guid.TryParseExact(userId, "D", out var id) ||
            id == Guid.Empty)
        {
            return null;
        }

        var canonicalUserId = id.ToString("D");

        try
        {
            // The authenticated account and session are checked
            // by TrustedAdministratorRoleResolutionService.
            // This reader only verifies trusted stored assignments.
            //
            // Fetch at most two active assignments so ambiguous
            // active-role records always fail closed.
            var activeAssignments = await _db.UserRoleAssignments
                .AsNoTracking()
                .Where(x =>
                    x.UserId == canonicalUserId &&
                    x.Status == "Active")
                .Select(x => new
                {
                    x.UserId,
                    x.Role,
                    x.Status,
                    x.RevokedAtUtc
                })
                .Take(2)
                .ToListAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (activeAssignments.Count != 1)
                return null;

            var assignment = activeAssignments[0];

            if (!Guid.TryParseExact(
                    assignment.UserId,
                    "D",
                    out var assignedId) ||
                assignedId != id)
            {
                return null;
            }

            if (!string.Equals(
                    assignment.Role,
                    "Admin",
                    StringComparison.Ordinal))
            {
                return null;
            }

            if (!string.Equals(
                    assignment.Status,
                    "Active",
                    StringComparison.Ordinal))
            {
                return null;
            }

            if (assignment.RevokedAtUtc is not null)
                return null;

            return new AdministratorRoleAssignment(
                canonicalUserId,
                "Admin",
                AdministratorRoleAssignmentStatus.Active);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is not OutOfMemoryException)
        {
            // Missing SQL table, unavailable DB or query failure:
            // no Administrator eligibility.
            return null;
        }
    }
}