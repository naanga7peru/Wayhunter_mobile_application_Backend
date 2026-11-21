using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.HunterPlaces;

/// <summary>Creates a Draft from trusted authentication and server-verified location data.</summary>
public sealed class HunterDraftCreationService(
    ITrustedIdentityAccountReader accounts,
    SqlHunterCaptureSubmissionAccessReader submissions,
    TimeProvider clock)
{
    public async Task<HunterSubmissionResponseDto> CreateAsync(
        string authenticatedUserId, string placeName, string? description,
        HunterVerifiedDraftLocation location, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(location);

        var account = await GetActiveAccountAsync(authenticatedUserId, cancellationToken);

        placeName = RequiredText(placeName, 200, nameof(placeName));
        var district = RequiredText(location.District, 100, nameof(location));
        description = description?.Trim();
        if (description?.Length > 2000)
            throw new ArgumentOutOfRangeException(nameof(description));

        var now = clock.GetUtcNow();
        if (!double.IsFinite(location.Latitude) || location.Latitude is < -90 or > 90 ||
            !double.IsFinite(location.Longitude) || location.Longitude is < -180 or > 180 ||
            !double.IsFinite(location.AccuracyMeters) || location.AccuracyMeters <= 0 ||
            location.CapturedAtUtc == default || location.CapturedAtUtc > now)
        {
            throw new ArgumentException("Invalid verified draft location.", nameof(location));
        }

        var draft = new HunterSubmission
        {
            UserId = account.UserId,
            UserNameSnapshot = RequiredText(account.DisplayName, 200, nameof(account)),
            PlaceName = placeName,
            Description = description,
            District = district,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            GpsAccuracyMeters = location.AccuracyMeters,
            LocationCapturedAtUtc = location.CapturedAtUtc.ToUniversalTime(),
            CreatedAtUtc = now
        };

        return await submissions.AddDraftAsync(draft, cancellationToken);
    }

    public async Task<bool> UpdateDetailsAsync(
        string authenticatedUserId, Guid submissionId,
        string placeName, string? description, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = await GetActiveAccountAsync(authenticatedUserId, cancellationToken);
        if (submissionId == Guid.Empty)
            throw new ArgumentException("Draft ID is required.", nameof(submissionId));

        return await submissions.UpdateDraftDetailsAsync(
            account.UserId, submissionId, RequiredText(placeName, 200, nameof(placeName)),
            ValidateDescription(description), cancellationToken);
    }

    private async Task<UserAccount> GetActiveAccountAsync(
        string authenticatedUserId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(authenticatedUserId, "D", out var ownerId) || ownerId == Guid.Empty)
            throw new UnauthorizedAccessException("Authenticated Hunter identity is required.");

        var canonicalOwnerId = ownerId.ToString("D");
        var account = await accounts.FindByUserIdAsync(canonicalOwnerId, cancellationToken);
        if (account is null || !TrustedIdentityAccessContract.IsActiveAccountOwner(canonicalOwnerId, account))
            throw new UnauthorizedAccessException("Authenticated Hunter identity is required.");
        return account;
    }

    private static string? ValidateDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (trimmed?.Length > 2000)
            throw new ArgumentOutOfRangeException(nameof(description));

        return trimmed;
    }

    private static string RequiredText(string? value, int maxLength, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
            throw new ArgumentException("Required text is missing or exceeds its storage limit.", parameterName);

        return trimmed;
    }
}

/// <summary>Internal input from a trusted location verifier. Never bind this type from an HTTP request.</summary>
public sealed record HunterVerifiedDraftLocation(
    string District, double Latitude, double Longitude, double AccuracyMeters, DateTimeOffset CapturedAtUtc);
