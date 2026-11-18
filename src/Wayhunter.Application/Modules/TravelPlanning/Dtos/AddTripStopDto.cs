
namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record AddTripStopDto(
    string Name,
    string? PlaceId = null,
    double? Latitude = null,
    double? Longitude = null,
    byte[]? ExpectedRowVersion = null
);
