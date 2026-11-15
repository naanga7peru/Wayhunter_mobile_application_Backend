
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Reacts to trusted M1 account status changes.
///
/// Suspended and deletion-requested accounts must have
/// their outstanding Hunter capture sessions revoked.
///
/// The account ID and status MUST originate from trusted
/// backend account persistence, never from Flutter input.
///
/// Production event wiring and transactional coordination
/// are not implemented here.
/// </summary>
public sealed class HunterAccountCaptureSessionRevocationService
{
    private readonly IHunterCaptureSessionRevocationStore
        _revocationStore;

    public HunterAccountCaptureSessionRevocationService(
        IHunterCaptureSessionRevocationStore revocationStore)
    {
        _revocationStore = revocationStore
            ?? throw new ArgumentNullException(
                nameof(revocationStore));
    }

    public Task<int> HandleAccountStatusChangedAsync(
        string? trustedAccountUserId,
        UserAccountStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // M1 UserAccount.UserId is a GUID-formatted string.
        // M4 uses the same logical identity as a Guid.
        if (string.IsNullOrWhiteSpace(trustedAccountUserId) ||
            !Guid.TryParseExact(
                trustedAccountUserId,
                "D",
                out var hunterId) ||
            hunterId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Trusted Hunter account identity is invalid.");
        }

        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus),
                "Unknown account status.");
        }

        // Only these account states trigger revocation.
        if (newStatus is not
            (UserAccountStatus.Suspended
            or UserAccountStatus.DeletionRequested))
        {
            return Task.FromResult(0);
        }

        // Revoke across ALL Hunter submissions belonging
        // to this account, not only one submission.
        //
        // Storage failures must propagate to the caller.
        return _revocationStore.RevokeByHunterIdAsync(
            hunterId,
            cancellationToken);
    }
}
