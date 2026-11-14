
using System;

namespace Wayhunter.Domain.Modules.HunterPlaces.Rules;

public static class HunterCaptureSessionRules
{
    public static void Validate(
        Guid authenticatedHunterId,
        Guid sessionHunterId,
        Guid requestedSubmissionId,
        Guid sessionSubmissionId,
        DateTimeOffset sessionIssuedAtUtc,
        DateTimeOffset sessionExpiresAtUtc,
        DateTimeOffset serverReceivedAtUtc,
        bool sessionAlreadyConsumed)
    {
        if (authenticatedHunterId == Guid.Empty ||
            sessionHunterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter IDs are required.");
        }

        if (requestedSubmissionId == Guid.Empty ||
            sessionSubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission IDs are required.");
        }

        if (sessionIssuedAtUtc == default ||
            sessionExpiresAtUtc == default ||
            serverReceivedAtUtc == default)
        {
            throw new ArgumentException(
                "Valid server timestamps are required.");
        }

        if (sessionIssuedAtUtc.Offset != TimeSpan.Zero ||
            sessionExpiresAtUtc.Offset != TimeSpan.Zero ||
            serverReceivedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Server timestamps must use UTC.");
        }

        if (sessionExpiresAtUtc <= sessionIssuedAtUtc)
        {
            throw new ArgumentException(
                "Session expiry must be after issuance.");
        }

        if (authenticatedHunterId != sessionHunterId)
        {
            throw new InvalidOperationException(
                "Capture session does not belong to this Hunter.");
        }

        if (requestedSubmissionId != sessionSubmissionId)
        {
            throw new InvalidOperationException(
                "Capture session belongs to another submission.");
        }

        if (sessionAlreadyConsumed)
        {
            throw new InvalidOperationException(
                "Capture session has already been used.");
        }

        if (serverReceivedAtUtc < sessionIssuedAtUtc)
        {
            throw new InvalidOperationException(
                "Capture session is not yet valid.");
        }

        if (serverReceivedAtUtc >= sessionExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "Capture session has expired.");
        }
    }
}
