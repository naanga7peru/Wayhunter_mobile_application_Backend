using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Validators;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

public sealed class HunterIdempotencyConflictException : Exception
{
    public HunterIdempotencyConflictException() : base("Idempotency key was already used with a different request.") { }
}

public sealed class HunterDraftService
{
    private readonly IHunterDraftStore _store;
    private readonly TimeProvider _clock;

    public HunterDraftService(IHunterDraftStore store, TimeProvider clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<HunterSubmissionResponseDto> CreateAsync(
        string authenticatedUserId, string usernameSnapshot,
        HunterDraftRequestDto request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var ownerId = RequireActor(authenticatedUserId);
        if (string.IsNullOrWhiteSpace(usernameSnapshot) || usernameSnapshot.Trim().Length > 200)
            throw new ArgumentException("A trusted account display name is required.", nameof(usernameSnapshot));
        var now = _clock.GetUtcNow();
        Validate(request, now);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128 ||
            idempotencyKey.Any(c => c < 0x21 || c > 0x7e))
            throw new ArgumentException("Idempotency-Key must contain 1–128 printable ASCII characters.", nameof(idempotencyKey));
        var submission = new HunterSubmission
        {
            Id = Guid.NewGuid(), UserId = ownerId.ToString("D"),
            UserNameSnapshot = usernameSnapshot.Trim(), Status = Domain.Modules.HunterPlaces.Enums.HunterSubmissionStatus.Draft,
            CreatedAtUtc = now
        };
        HunterDraftRequestValidator.Apply(request, submission);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            PlaceName = request.PlaceName.Trim(),
            Description = request.Description?.Trim(),
            ClaimedDistrict = request.ClaimedDistrict.Trim(),
            request.Latitude,
            request.Longitude,
            request.GpsAccuracyMeters,
            request.LocationCapturedAtUtc
        });
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)));
        var requestDigest = Convert.ToHexString(SHA256.HashData(canonical));
        var stored = await _store.CreateAsync(submission, keyHash, requestDigest, cancellationToken);
        return Map(stored);
    }

    public async Task<HunterDraftUpdateResult> UpdateAsync(string authenticatedUserId, Guid submissionId,
        HunterDraftRequestDto request, CancellationToken cancellationToken = default)
    {
        var owner = RequireActor(authenticatedUserId);
        Validate(request, _clock.GetUtcNow());
        return await _store.UpdateDraftAsync(submissionId, owner.ToString("D"), request, cancellationToken);
    }

    public Task<HunterDraftDeleteResult> DeleteAsync(string authenticatedUserId,
        Guid submissionId, CancellationToken cancellationToken = default) =>
        _store.DeleteDraftAsync(submissionId, RequireActor(authenticatedUserId).ToString("D"), cancellationToken);

    private static Guid RequireActor(string actor)
    {
        if (!Guid.TryParseExact(actor, "D", out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("A trusted account identity is required.");
        return userId;
    }

    private static void Validate(HunterDraftRequestDto request, DateTimeOffset now)
    {
        var errors = HunterDraftRequestValidator.Validate(request, now);
        if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors), nameof(request));
    }

    private static HunterSubmissionResponseDto Map(HunterSubmission row) => new()
    {
        SubmissionId = row.Id, PlaceName = row.PlaceName, District = row.District,
        Status = row.Status, CreatedAtUtc = row.CreatedAtUtc, SubmittedAtUtc = row.SubmittedAtUtc
    };
}
