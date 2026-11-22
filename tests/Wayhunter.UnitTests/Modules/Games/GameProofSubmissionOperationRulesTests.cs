using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProofSubmissionOperationRulesTests
{
    private static GameProofSubmissionOperationBinding Valid() => new(Guid.NewGuid(), "player-1", Guid.NewGuid(), Guid.NewGuid(), 1, "capture-1", "provider-1", "media-1");

    [Fact]
    public void ValidBinding_ShouldPass() => GameProofSubmissionOperationRules.Validate(Valid());

    [Fact]
    public void EmptyOperationId_ShouldReject()
    {
        var binding = Valid() with { SubmissionOperationId = Guid.Empty };
        Assert.Throws<ArgumentException>(() => GameProofSubmissionOperationRules.Validate(binding));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidStep_ShouldReject(int step)
    {
        var binding = Valid() with { StepNumber = step };
        Assert.Throws<ArgumentException>(() => GameProofSubmissionOperationRules.Validate(binding));
    }

    [Fact]
    public void MissingTrustedPlayer_ShouldReject()
    {
        var binding = Valid() with { AuthenticatedPlayerUserId = " " };
        Assert.Throws<ArgumentException>(() => GameProofSubmissionOperationRules.Validate(binding));
    }

    [Fact]
    public void OversizedMediaId_ShouldReject()
    {
        var binding = Valid() with { CanonicalMediaId = new string((char)120, 257) };
        Assert.Throws<ArgumentException>(() => GameProofSubmissionOperationRules.Validate(binding));
    }

    [Fact]
    public void MatchingBinding_ShouldPass()
    {
        var binding = Valid();
        GameProofSubmissionOperationRules.EnsureMatchingBinding(binding, binding with { });
    }

    [Fact]
    public void ConflictingCapture_ShouldReject()
    {
        var binding = Valid();
        var changed = binding with { CaptureSessionId = "other-capture" };
        Assert.Throws<InvalidOperationException>(() => GameProofSubmissionOperationRules.EnsureMatchingBinding(binding, changed));
    }
    [Fact]
    public void StoredOperation_MatchingBinding_ShouldPass()
    {
        var binding = Valid();
        var now = DateTimeOffset.UtcNow;
        var stored = new GameProofSubmissionOperationSnapshot(binding, GameProofSubmissionOperationState.Pending, null, null, null, null, null, 0, null, now, now);
        GameProofSubmissionOperationRules.EnsureMatchingStoredOperation(stored, binding with { });
    }

    [Fact]
    public void StoredOperation_ConflictingBinding_ShouldReject()
    {
        var binding = Valid();
        var now = DateTimeOffset.UtcNow;
        var stored = new GameProofSubmissionOperationSnapshot(binding, GameProofSubmissionOperationState.Pending, null, null, null, null, null, 0, null, now, now);
        var conflicts = new[] {
            binding with { AuthenticatedPlayerUserId = "other-player" },
            binding with { GameId = Guid.NewGuid() },
            binding with { CaptureSessionId = "other-capture" },
            binding with { CanonicalMediaId = "other-media" }
        };
        foreach (var conflict in conflicts)
            Assert.Throws<InvalidOperationException>(() => GameProofSubmissionOperationRules.EnsureMatchingStoredOperation(stored, conflict));
    }
}
