using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;

public sealed class Campaign
{
    public Guid Id { get; }
    public CampaignType Type { get; }
    public Guid SponsorId { get; }
    public Guid ContentId { get; }
    public string? TargetDistrict { get; }
    public long BudgetCoins { get; }
    public DateTimeOffset StartAtUtc { get; }
    public DateTimeOffset EndAtUtc { get; }
    public CampaignStatus Status { get; private set; }

    public Campaign(
        Guid id,
        CampaignType type,
        Guid sponsorId,
        Guid contentId,
        string? targetDistrict,
        long budgetCoins,
        DateTimeOffset startAt,
        DateTimeOffset endAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(sponsorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(contentId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(budgetCoins);

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (endAt <= startAt)
            throw new ArgumentException("End time must be after start time.", nameof(endAt));

        if (targetDistrict is not null && string.IsNullOrWhiteSpace(targetDistrict))
            throw new ArgumentException("Target district must have a value.", nameof(targetDistrict));
        if (type == CampaignType.DistrictPass && targetDistrict is null)
            throw new ArgumentException("A district pass requires a target district.", nameof(targetDistrict));

        Id = id;
        Type = type;
        SponsorId = sponsorId;
        ContentId = contentId;
        TargetDistrict = targetDistrict?.Trim();
        BudgetCoins = budgetCoins;
        StartAtUtc = startAt.ToUniversalTime();
        EndAtUtc = endAt.ToUniversalTime();
    }

    public void Schedule(DateTimeOffset now)
    {
        if (Status != CampaignStatus.Draft || now >= StartAtUtc)
            throw new InvalidOperationException("Only drafts with a future start can be scheduled.");

        Status = CampaignStatus.Scheduled;
    }

    public void Publish(DateTimeOffset now, bool isContentEligible)
    {
        if (Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled or CampaignStatus.Paused) ||
            now >= EndAtUtc || !isContentEligible)
        {
            throw new InvalidOperationException("The campaign or its linked content is ineligible for publication.");
        }

        Status = CampaignStatus.Published;
    }

    public void Pause()
    {
        if (Status != CampaignStatus.Published)
            throw new InvalidOperationException("Only published campaigns can be paused.");

        Status = CampaignStatus.Paused;
    }

    public void Archive() => Status = CampaignStatus.Archived;

    public bool IsAvailable(DateTimeOffset now, string? district, bool isContentEligible)
    {
        return Status == CampaignStatus.Published &&
            now >= StartAtUtc && now < EndAtUtc &&
            isContentEligible &&
            (TargetDistrict is null ||
                string.Equals(TargetDistrict, district?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
