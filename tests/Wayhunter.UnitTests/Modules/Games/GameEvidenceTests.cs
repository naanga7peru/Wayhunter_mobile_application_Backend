
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameEvidenceTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evidence_ShouldStoreCorrectValues()
    {
        var evidenceId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        var evidence = new GameEvidence(
            evidenceId,
            attemptId,
            1,
            "camera-photo-001",
            SubmittedAt);

        Assert.Equal(evidenceId, evidence.Id);
        Assert.Equal(attemptId, evidence.AttemptId);
        Assert.Equal(1, evidence.StepNumber);
        Assert.Equal("camera-photo-001", evidence.MediaId);
        Assert.Equal(SubmittedAt, evidence.SubmittedAtUtc);
    }

    [Fact]
    public void Evidence_ShouldRejectEmptyId()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameEvidence(
                Guid.Empty,
                Guid.NewGuid(),
                1,
                "photo-001",
                SubmittedAt));
    }

    [Fact]
    public void Evidence_ShouldRejectEmptyAttemptId()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameEvidence(
                Guid.NewGuid(),
                Guid.Empty,
                1,
                "photo-001",
                SubmittedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Evidence_ShouldRejectInvalidStepNumber(
        int stepNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GameEvidence(
                Guid.NewGuid(),
                Guid.NewGuid(),
                stepNumber,
                "photo-001",
                SubmittedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Evidence_ShouldRejectEmptyMediaId(
        string mediaId)
    {
        Assert.Throws<ArgumentException>(() =>
            new GameEvidence(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                mediaId,
                SubmittedAt));
    }

    [Fact]
    public void Evidence_ShouldRejectNonUtcTimestamp()
    {
        var nonUtcTime = new DateTimeOffset(
            2026, 11, 13, 15, 30, 0,
            TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            new GameEvidence(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "photo-001",
                nonUtcTime));
    }

    [Fact]
    public void Evidence_ShouldTrimMediaId()
    {
        var evidence = new GameEvidence(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "  photo-002  ",
            SubmittedAt);

        Assert.Equal("photo-002", evidence.MediaId);
    }
}
