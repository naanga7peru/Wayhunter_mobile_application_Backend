using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterEvidenceReceiptCandidateTests
{
    [Theory]
    [InlineData("matching", true)]
    [InlineData("missing", false)]
    [InlineData("foreign-owner", false)]
    [InlineData("foreign-submission", false)]
    [InlineData("foreign-session", false)]
    [InlineData("wrong-type", false)]
    public async Task VerifyBinding_RequiresProviderReceiptForExactOperation(string scenario, bool accepted)
    {
        var upload = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var submission = Guid.NewGuid();
        var session = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var receipt = new HunterVerifiedUploadReceipt(upload,
            scenario == "foreign-owner" ? Guid.NewGuid() : owner,
            scenario == "foreign-submission" ? Guid.NewGuid() : submission,
            scenario == "foreign-session" ? Guid.NewGuid() : session,
            "test-canonical-media", new HunterEvidence
            {
                HunterSubmissionId = submission,
                EvidenceType = scenario == "wrong-type" ? HunterEvidenceType.LivePhoto : HunterEvidenceType.Guided360Video,
                CapturedAtUtc = now,
                UploadedAtUtc = now,
                PrivateMediaReference = "test-private-reference"
            });

        var candidate = new HunterEvidenceReceiptCandidate(new Verifier(scenario == "missing" ? null : receipt));
        var result = await candidate.VerifyBindingAsync(
            upload, owner, submission, session, HunterEvidenceType.Guided360Video);

        Assert.Equal(accepted, result is not null);
    }

    private sealed class Verifier(HunterVerifiedUploadReceipt? receipt) : IHunterUploadReceiptVerifier
    {
        public Task<HunterVerifiedUploadReceipt?> VerifyAsync(Guid uploadId, CancellationToken cancellationToken) =>
            Task.FromResult(receipt);
    }
}
