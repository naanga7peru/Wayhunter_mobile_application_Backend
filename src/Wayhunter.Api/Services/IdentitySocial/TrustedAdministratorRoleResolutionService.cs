using System.Security.Claims;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class TrustedAdministratorRoleResolutionService
{
    private const string AdministratorRole = "Admin";

    private readonly AuthenticatedSessionAuthorizationService
        _sessionAuthorization;

    private readonly ITrustedAdministratorRoleReader
        _roleReader;

    public TrustedAdministratorRoleResolutionService(
        AuthenticatedSessionAuthorizationService sessionAuthorization,
        ITrustedAdministratorRoleReader roleReader)
    {
        _sessionAuthorization = sessionAuthorization
            ?? throw new ArgumentNullException(
                nameof(sessionAuthorization));

        _roleReader = roleReader
            ?? throw new ArgumentNullException(
                nameof(roleReader));
    }

    public async Task<Claim?> ResolveAdminRoleClaimAsync(
        string? bearerToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(bearerToken))
            return null;

        try
        {
            // M1 validates JWT, active verified account,
            // persisted session ownership and revocation.
            var identity = await _sessionAuthorization
                .AuthorizeAsync(
                    bearerToken,
                    cancellationToken);

            if (identity is null)
                return null;

            // The source must be server-controlled.
            // Never use claims supplied by the client.
            var assignment = await _roleReader
                .FindAssignmentAsync(
                    identity.UserId,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (assignment is null)
                return null;

            if (!Guid.TryParseExact(
                    identity.UserId,
                    "D",
                    out var authenticatedId) ||
                authenticatedId == Guid.Empty)
            {
                return null;
            }

            if (!Guid.TryParseExact(
                    assignment.UserId,
                    "D",
                    out var assignedId) ||
                assignedId != authenticatedId)
            {
                return null;
            }

            if (assignment.Status !=
                AdministratorRoleAssignmentStatus.Active)
            {
                return null;
            }

            if (!string.Equals(
                    assignment.Role,
                    AdministratorRole,
                    StringComparison.Ordinal))
            {
                return null;
            }

            // Approved M1-M7 role claim contract.
            return new Claim(
                ClaimTypes.Role,
                AdministratorRole);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is not OutOfMemoryException)
        {
            // Fail closed on authentication or role-source
            // failures. No Administrator privilege is granted.
            // Production observability is required before
            // this resolver is integrated into the pipeline.
            return null;
        }
    }
}
