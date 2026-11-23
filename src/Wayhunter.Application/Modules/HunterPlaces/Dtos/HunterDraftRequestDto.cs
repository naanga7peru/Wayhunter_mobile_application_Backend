namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

public sealed class HunterDraftRequestDto
{
    public string PlaceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ClaimedDistrict { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? GpsAccuracyMeters { get; set; }
    public DateTimeOffset? LocationCapturedAtUtc { get; set; }
}
