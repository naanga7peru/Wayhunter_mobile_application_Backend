using System;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Infrastructure.Persistence.Models.Games;

/// <summary>
/// Unregistered Design v3 registry entity proposal ONLY. Do not add to the
/// shared DbContext, migration, runtime DI, or execute SQL before sign-off.
/// </summary>
public sealed class GameProofMediaClaimRow
{
    public Guid MediaClaimId { get; set; }
    public string MediaProviderNamespace { get; set; } = string.Empty;
    public string CanonicalMediaId { get; set; } = string.Empty;
    public Guid? SubmissionOperationId { get; set; }
    public Guid? EvidenceId { get; set; }
    public Guid GameId { get; set; }
    public Guid AttemptId { get; set; }
    public int StepNumber { get; set; }
    public string AuthenticatedPlayerUserId { get; set; } = string.Empty;
    public GameProofMediaClaimOrigin ClaimOrigin { get; set; }
    public GameProofMediaClaimState ClaimState { get; set; }
    public DateTimeOffset? ProviderConsumedAtUtc { get; set; }
    public DateTimeOffset? EvidenceCommittedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
