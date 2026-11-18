
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

    public AdvertisementPlacement Placement { get; private set; }
    public AdvertisementStatus Status { get; private set; }

    public long ImpressionCount { get; private set; }
    public long ClickCount { get; private set; }

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
        Placement = AdvertisementPlacement.HomeBanner;
        Status = AdvertisementStatus.Draft;

        ImpressionCount = 0;
        ClickCount = 0;
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

    // M7 - Admin Advertisement Update
    public void UpdateDetails(
        string title,
        string imageUrl,
        string? destinationUrl,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        AdvertisementPlacement placement)
    {
        if (Status == AdvertisementStatus.Archived)
        {
            throw new InvalidOperationException(
                "Archived advertisements cannot be edited.");
        }

        if (string.IsNullOrWhiteSpace(title) ||
            title.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Advertisement title must contain 1-200 characters.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(imageUrl) ||
            imageUrl.Trim().Length > 2048)
        {
            throw new ArgumentException(
                "Invalid advertisement image URL.",
                nameof(imageUrl));
        }

        if (destinationUrl?.Trim().Length > 2048)
        {
            throw new ArgumentException(
                "Advertisement destination URL is too long.",
                nameof(destinationUrl));
        }

        if (startAtUtc == default ||
            endAtUtc == default ||
            startAtUtc.Offset != TimeSpan.Zero ||
            endAtUtc.Offset != TimeSpan.Zero ||
            endAtUtc <= startAtUtc)
        {
            throw new ArgumentException(
                "Invalid advertisement UTC date window.");
        }

        if (placement != AdvertisementPlacement.HomeBanner &&
            placement != AdvertisementPlacement.SponsoredPoi)
        {
            throw new ArgumentException(
                "Invalid advertisement placement.",
                nameof(placement));
        }

        Title = title.Trim();
        ImageUrl = imageUrl.Trim();
        DestinationUrl = destinationUrl?.Trim();
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
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

    public void RecordImpression()
    {
        checked
        {
            ImpressionCount++;
        }
    }

    public void RecordClick()
    {
        checked
        {
            ClickCount++;
        }
    }
}
