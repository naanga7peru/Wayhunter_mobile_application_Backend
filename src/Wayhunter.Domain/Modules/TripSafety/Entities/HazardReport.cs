
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

    // Optional for historical rows created before
    // the location/evidence schema was approved.
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? Description { get; private set; }
    public string? PhotoAssetReference { get; private set; }

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

    // Existing factory retained for backward compatibility
    // with the existing M3 domain tests.
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
            throw new ArgumentException(
                "Photo capture time is required.");

        return new HazardReport(
            id,
            reporterUserId.Trim(),
            serverTimeUtc.ToUniversalTime(),
            photoCapturedAtUtc.ToUniversalTime());
    }

    public static HazardReport ReportWithEvidence(
        Guid id,
        string reporterUserId,
        DateTimeOffset serverTimeUtc,
        DateTimeOffset photoCapturedAtUtc,
        double latitude,
        double longitude,
        string description,
        string photoAssetReference)
    {
        if (!double.IsFinite(latitude) ||
            latitude < -90 || latitude > 90)
        {
            throw new ArgumentException("Invalid latitude.");
        }

        if (!double.IsFinite(longitude) ||
            longitude < -180 || longitude > 180)
        {
            throw new ArgumentException("Invalid longitude.");
        }

        if (string.IsNullOrWhiteSpace(description) ||
            description.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Hazard description must contain 1 to 1000 characters.");
        }

        if (string.IsNullOrWhiteSpace(photoAssetReference) ||
            photoAssetReference.Trim().Length > 512)
        {
            throw new ArgumentException(
                "Valid private photo asset reference is required.");
        }

        // Store only an opaque asset identifier, not a URL.
        if (Uri.TryCreate(
            photoAssetReference.Trim(),
            UriKind.Absolute,
            out _))
        {
            throw new ArgumentException(
                "Photo reference must not be a public URL.");
        }

        var report = Report(
            id,
            reporterUserId,
            serverTimeUtc,
            photoCapturedAtUtc);

        // Camera metadata comes from the client.
        // It must never determine 24-hour expiry.
        if (report.PhotoCapturedAtUtc > report.ReportedAtUtc)
        {
            throw new ArgumentException(
                "Photo capture time cannot be in the future.");
        }

        report.Latitude = latitude;
        report.Longitude = longitude;
        report.Description = description.Trim();
        report.PhotoAssetReference = photoAssetReference.Trim();

        return report;
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
        {
            throw new InvalidOperationException(
                "Only reported hazards can be verified.");
        }

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
            throw new ArgumentException(
                "Resolution reason is required.");

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
        {
            return;
        }

        if (now < ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "Hazard has not reached expiry.");
        }

        Status = HazardStatus.Expired;
        ExpiredAtUtc = now;
    }

    public bool IsWarningActive(DateTimeOffset serverTimeUtc)
    {
        var now = ValidateTime(serverTimeUtc);

        return (Status == HazardStatus.Reported ||
                Status == HazardStatus.Verified) &&
               now >= ReportedAtUtc &&
               now < ExpiresAtUtc;
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
