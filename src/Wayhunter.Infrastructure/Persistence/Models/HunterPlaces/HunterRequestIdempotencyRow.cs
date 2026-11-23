namespace Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

public sealed class HunterRequestIdempotencyRow
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string RequestDigest { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
