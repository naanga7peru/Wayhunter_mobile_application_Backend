
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
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }
}
