
using System;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Domain.Modules.TripSafety.Entities;

public sealed class HazardReport
{
    public Guid Id { get; }

    public string ReporterUserId { get; }

    public HazardStatus Status { get; private set; }

    public DateTimeOffset ReportedAtUtc { get; }

    public DateTimeOffset PhotoCapturedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public DateTimeOffset? ExpiredAtUtc { get; private set; }

    public string? VerifiedByUserId { get; private set; }

    public string? ResolvedByUserId { get; private set; }

    public string? ResolutionReason { get; private set; }

    private HazardReport(
        Guid id,
        string reporterUserId,
        DateTimeOffset reportedAtUtc,
        DateTimeOffset photoCapturedAtUtc)
    {
        Id = id;
        ReporterUserId = reporterUserId;
        ReportedAtUtc = reportedAtUtc;
        PhotoCapturedAtUtc = photoCapturedAtUtc;

        ExpiresAtUtc = reportedAtUtc.AddHours(24);

        Status = HazardStatus.Reported;
    }

    public static HazardReport Report(
        Guid id,
        string reporterUserId,
        DateTimeOffset serverTimeUtc,
        DateTimeOffset photoCapturedAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Invalid Hazard ID.");

        if (string.IsNullOrWhiteSpace(reporterUserId))
            throw new ArgumentException("Reporter ID is required.");

        if (serverTimeUtc == default)
            throw new ArgumentException("Server time is required.");

        if (photoCapturedAtUtc == default)
            throw new ArgumentException("Photo capture time is required.");

        return new HazardReport(
            id,
            reporterUserId.Trim(),
            serverTimeUtc.ToUniversalTime(),
            photoCapturedAtUtc.ToUniversalTime());
    }

    public void Verify(
        string reviewerUserId,
        DateTimeOffset serverTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("Reviewer ID is required.");

        if (string.Equals(
            reviewerUserId.Trim(),
            ReporterUserId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Reporter cannot verify own hazard.");
        }

        var now = ValidateTime(serverTimeUtc);

        EnsureActive(now);

        if (Status != HazardStatus.Reported)
            throw new InvalidOperationException(
                "Only reported hazards can be verified.");

        Status = HazardStatus.Verified;
        VerifiedAtUtc = now;
        VerifiedByUserId = reviewerUserId.Trim();
    }

    public void Resolve(
        string reviewerUserId,
        string reason,
        DateTimeOffset serverTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("Reviewer ID is required.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Resolution reason is required.");

        var now = ValidateTime(serverTimeUtc);

        EnsureActive(now);

        Status = HazardStatus.Resolved;
        ResolvedAtUtc = now;
        ResolvedByUserId = reviewerUserId.Trim();
        ResolutionReason = reason.Trim();
    }

    public void Expire(DateTimeOffset serverTimeUtc)
    {
        var now = ValidateTime(serverTimeUtc);

        if (Status == HazardStatus.Expired ||
            Status == HazardStatus.Resolved)
            return;

        if (now < ExpiresAtUtc)
            throw new InvalidOperationException(
                "Hazard warning has not reached expiry.");

        Status = HazardStatus.Expired;
        ExpiredAtUtc = now;
    }

    public bool IsWarningActive(DateTimeOffset serverTimeUtc)
    {
        var now = ValidateTime(serverTimeUtc);

        return (Status == HazardStatus.Reported ||
                Status == HazardStatus.Verified)
            && now >= ReportedAtUtc
            && now < ExpiresAtUtc;
    }

    private void EnsureActive(DateTimeOffset now)
    {
        if (Status == HazardStatus.Resolved ||
            Status == HazardStatus.Expired ||
            now >= ExpiresAtUtc ||
            now < ReportedAtUtc)
        {
            throw new InvalidOperationException(
                "Hazard is not active.");
        }
    }

    private static DateTimeOffset ValidateTime(
        DateTimeOffset time)
    {
        if (time == default)
            throw new ArgumentException(
                "Valid server time is required.");

        return time.ToUniversalTime();
    }
}
