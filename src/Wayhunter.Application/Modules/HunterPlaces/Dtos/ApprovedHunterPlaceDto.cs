namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

public sealed record ApprovedHunterPlaceDto(
    Guid SubmissionId,
    string PlaceName,
    string District,
    double Latitude,
    double Longitude,
    DateTimeOffset ApprovedAtUtc);
