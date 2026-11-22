using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;
namespace Wayhunter.UnitTests.Modules.Games;
public sealed class GameProofCaptureReceiptLengthTests
{
    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void ConsumedReceipt_LengthMatchesSqlLimit(int length, bool accepted)
    {
        var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), "player-1", Guid.NewGuid(), Guid.NewGuid(), 1, "capture-1", "provider-1", "media-1");
        var outcome = new GameProofCaptureOperationOutcome(binding.SubmissionOperationId, GameProofCaptureOperationOutcomeStatus.Consumed, new string('r', length));
        if (accepted) GameProofCaptureOutcomeRules.Validate(binding, outcome);
        else Assert.Throws<InvalidOperationException>(() => GameProofCaptureOutcomeRules.Validate(binding, outcome));
    }
}
