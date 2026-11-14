
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;

public sealed class Advertisement
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string ImageUrl { get; private set; }
    public string? DestinationUrl { get; private set; }
    public DateTimeOffset StartAtUtc { get; private set; }
    public DateTimeOffset EndAtUtc { get; private set; }
    public bool IsEnabled { get; private set; }

    // Where the advertisement should appear
    public AdvertisementPlacement Placement { get; private set; }

    // Current advertisement status
    public AdvertisementStatus Status { get; private set; }

    public Advertisement(
        Guid id,
        string title,
        string imageUrl,
        string? destinationUrl,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Invalid advertisement ID.", nameof(id));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException(
                "Image URL is required.", nameof(imageUrl));

        if (endAtUtc <= startAtUtc)
            throw new ArgumentException(
                "End time must be after start time.");

        Id = id;
        Title = title.Trim();
        ImageUrl = imageUrl.Trim();
        DestinationUrl = destinationUrl;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;

        IsEnabled = false;

        // Default placement
        Placement = AdvertisementPlacement.HomeBanner;

        // Default status
        Status = AdvertisementStatus.Draft;
    }

    public void SetPlacement(AdvertisementPlacement placement)
    {
        if (!Enum.IsDefined(placement) ||
            placement == AdvertisementPlacement.Unspecified)
        {
            throw new ArgumentException(
                "Invalid advertisement placement.",
                nameof(placement));
        }

        Placement = placement;
    }

    public void Enable()
    {
        if (Status == AdvertisementStatus.Archived)
        {
            throw new InvalidOperationException(
                "Archived advertisements cannot be activated.");
        }

        IsEnabled = true;
        Status = AdvertisementStatus.Active;
    }

    public void Disable()
    {
        IsEnabled = false;

        if (Status == AdvertisementStatus.Active)
        {
            Status = AdvertisementStatus.Paused;
        }
    }

    public void Archive()
    {
        IsEnabled = false;
        Status = AdvertisementStatus.Archived;
    }
}
