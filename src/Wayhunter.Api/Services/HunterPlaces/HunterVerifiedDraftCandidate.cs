using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Api.Services.HunterPlaces;

// Provisional GEO-01 boundary. The production host registers no implementation.
public interface IHunterLocationVerifier
{
    Task<HunterLocationVerification> VerifyAsync(
        HunterLocationSample sample, CancellationToken cancellationToken);
}

public sealed record HunterLocationSample(
    double Latitude, double Longitude, double AccuracyMeters, DateTimeOffset CapturedAtUtc);

public enum HunterLocationOutcome
{
    Unavailable,
    Verified,
    OutsideCoverage,
    Ineligible
}

public sealed record HunterLocationVerification(HunterLocationOutcome Outcome, string? District = null);

public sealed class HunterVerifiedDraftCandidate(
    IHunterLocationVerifier verifier, HunterDraftCreationService drafts, TimeProvider clock)
{
    public async Task<(HunterLocationOutcome Outcome, HunterSubmissionResponseDto? Draft)> CreateAsync(
        string authenticatedUserId, string placeName, string? description,
        HunterLocationSample sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(sample.Latitude) || sample.Latitude is < -90 or > 90 ||
            !double.IsFinite(sample.Longitude) || sample.Longitude is < -180 or > 180 ||
            !double.IsFinite(sample.AccuracyMeters) || sample.AccuracyMeters <= 0 ||
            sample.CapturedAtUtc == default || sample.CapturedAtUtc > clock.GetUtcNow())
        {
            throw new ArgumentException("Invalid location sample.", nameof(sample));
        }

        var verification = await verifier.VerifyAsync(sample, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (verification.Outcome != HunterLocationOutcome.Verified)
            return (verification.Outcome, null);

        if (string.IsNullOrWhiteSpace(verification.District))
            return (HunterLocationOutcome.Unavailable, null);

        var location = new HunterVerifiedDraftLocation(
            verification.District, sample.Latitude, sample.Longitude, sample.AccuracyMeters, sample.CapturedAtUtc);
        return (HunterLocationOutcome.Verified,
            await drafts.CreateAsync(authenticatedUserId, placeName, description, location, cancellationToken));
    }
}
