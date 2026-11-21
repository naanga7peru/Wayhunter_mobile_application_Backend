using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCommittedRewardSourceTests
{
    [Fact]
    public async Task Reconciliation_RequiresApprovedSubmissionAndReviewAndExcludesOutboxReplay()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("D")).Options;
        await using var db = new WayhunterSharedDbContext(options);
        var now = DateTimeOffset.UtcNow;
        var owner = Guid.NewGuid().ToString("D");
        var eligible = new HunterSubmission { UserId = owner, Status = HunterSubmissionStatus.Approved };
        var noReview = new HunterSubmission { UserId = owner, Status = HunterSubmissionStatus.Approved };
        var held = new HunterSubmission { UserId = owner, Status = HunterSubmissionStatus.Held };
        var rejected = new HunterSubmission { UserId = owner, Status = HunterSubmissionStatus.Rejected };
        db.HunterSubmissions.AddRange(eligible, noReview, held, rejected);
        foreach (var submission in new[] { eligible, held, rejected })
            db.HunterReviewDecisions.Add(new HunterReviewDecision
            {
                HunterSubmissionId = submission.Id, ResultingStatus = HunterSubmissionStatus.Approved,
                ReviewedAtUtc = now, ReviewerUserId = Guid.NewGuid().ToString("D")
            });
        await db.SaveChangesAsync();

        var source = new SqlCommittedCoinRewardSource(db, "2026-11-11");
        var reward = Assert.Single(await source.FindCommittedRewardCandidatesAsync(10));
        Assert.Equal(CoinRewardType.HunterApproved, reward.RewardType);
        Assert.Equal(owner, reward.RecipientUserId);
        Assert.Equal(eligible.Id.ToString("D"), reward.SourceRecordId);
        Assert.Equal(200, reward.CoinsAmount);
        Assert.Equal(reward.EventId, Assert.Single(await source.FindCommittedRewardCandidatesAsync(10)).EventId);

        db.CoinRewardOutbox.Add(new CoinRewardOutboxRow
        {
            RewardType = CoinRewardType.HunterApproved, SourceRecordId = eligible.Id.ToString("D"),
            RecipientUserId = owner
        });
        await db.SaveChangesAsync();
        Assert.Empty(await source.FindCommittedRewardCandidatesAsync(10));
    }
}
