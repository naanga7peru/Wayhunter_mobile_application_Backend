using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProofSubmissionOperationLengthTests
{
    private static GameProofSubmissionOperationBinding WithLength(string field, int length)
    {
        var b = new GameProofSubmissionOperationBinding(Guid.NewGuid(), "player-1", Guid.NewGuid(), Guid.NewGuid(), 1, "capture-1", "provider-1", "media-1");
        var value = new string('x', length);
        return field switch
        {
            "player" => b with { AuthenticatedPlayerUserId = value },
            "capture" => b with { CaptureSessionId = value },
            "provider" => b with { MediaProviderNamespace = value },
            "media" => b with { CanonicalMediaId = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
    }

    [Theory]
    [InlineData("player", 256)]
    [InlineData("capture", 256)]
    [InlineData("provider", 64)]
    [InlineData("media", 256)]
    public void MaximumLength_ShouldPass(string field, int max) => GameProofSubmissionOperationRules.Validate(WithLength(field, max));

    [Theory]
    [InlineData("player", 256)]
    [InlineData("capture", 256)]
    [InlineData("provider", 64)]
    [InlineData("media", 256)]
    public void OverMaximumLength_ShouldReject(string field, int max) => Assert.Throws<ArgumentException>(() => GameProofSubmissionOperationRules.Validate(WithLength(field, max + 1)));
}
