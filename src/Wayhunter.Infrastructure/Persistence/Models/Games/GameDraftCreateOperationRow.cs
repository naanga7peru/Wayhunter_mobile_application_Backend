namespace Wayhunter.Infrastructure.Persistence.Models.Games;

public sealed class GameDraftCreateOperationRow
{
    public string CreatorUserId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public byte[] PayloadHash { get; set; } = [];
    public Guid? GameId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
