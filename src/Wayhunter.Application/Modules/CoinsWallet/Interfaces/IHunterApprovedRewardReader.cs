
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

/// <summary>
/// Trusted, server-side M4 Hunter approval snapshot.
/// The implementation must read committed Admin review data,
/// not client-supplied approval flags.
/// </summary>
public sealed record HunterApprovedRewardSnapshot(
    Guid SubmissionId,
    string HunterUserId,
    HunterSubmissionStatus SubmissionStatus,
    DateTimeOffset? SubmittedAtUtc,
    Guid ReviewId,
    Guid ReviewSubmissionId,
    HunterSubmissionStatus ReviewStatus,
    string ReviewerAdminUserId,
    DateTimeOffset ReviewedAtUtc
);

public interface IHunterApprovedRewardReader
{
    Task<HunterApprovedRewardSnapshot?> FindBySubmissionIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);
}
