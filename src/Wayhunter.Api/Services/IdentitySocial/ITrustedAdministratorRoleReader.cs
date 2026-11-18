namespace Wayhunter.Api.Services.IdentitySocial;

public enum AdministratorRoleAssignmentStatus
{
    Active = 1,
    Revoked = 2,
    Inactive = 3
}

public sealed record AdministratorRoleAssignment(
    string UserId,
    string Role,
    AdministratorRoleAssignmentStatus Status);

public interface ITrustedAdministratorRoleReader
{
    // Must read from an approved server-controlled source.
    // Never accept client-provided role assignments.
    // Null means no assignment.
    Task<AdministratorRoleAssignment?> FindAssignmentAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
