
namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

public sealed record AdvertisementDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public string? DestinationUrl { get; init; }

    public DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset EndAtUtc { get; init; }

    public bool IsEnabled { get; init; }
}
