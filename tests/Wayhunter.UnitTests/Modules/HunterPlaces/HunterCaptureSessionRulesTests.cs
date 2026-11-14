
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCaptureSessionRulesTests
{
    private static readonly Guid HunterId = Guid.NewGuid();
    private static readonly Guid SubmissionId = Guid.NewGuid();

    private static readonly DateTimeOffset IssuedAt =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpiresAt =
        IssuedAt.AddMinutes(10);

    private static readonly DateTimeOffset ReceivedAt =
        IssuedAt.AddMinutes(5);

    private static void Validate(
        Guid? authenticatedHunterId = null,
        Guid? sessionHunterId = null,
        Guid? requestedSubmissionId = null,
        Guid? sessionSubmissionId = null,
        DateTimeOffset? issuedAtUtc = null,
        DateTimeOffset? expiresAtUtc = null,
        DateTimeOffset? receivedAtUtc = null,
        bool consumed = false)
    {
        HunterCaptureSessionRules.Validate(
            authenticatedHunterId ?? HunterId,
            sessionHunterId ?? HunterId,
            requestedSubmissionId ?? SubmissionId,
            sessionSubmissionId ?? SubmissionId,
            issuedAtUtc ?? IssuedAt,
            expiresAtUtc ?? ExpiresAt,
            receivedAtUtc ?? ReceivedAt,
            consumed);
    }

    [Fact]
    public void ValidActiveSession_ShouldPass()
    {
        Validate();
    }

    [Fact]
    public void SessionAtIssueTime_ShouldPass()
    {
        Validate(receivedAtUtc: IssuedAt);
    }

    [Fact]
    public void SessionJustBeforeExpiry_ShouldPass()
    {
        Validate(
            receivedAtUtc: ExpiresAt.AddTicks(-1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EmptyRequiredId_ShouldThrow(int field)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            switch (field)
            {
                case 0:
                    Validate(authenticatedHunterId: Guid.Empty);
                    break;

                case 1:
                    Validate(sessionHunterId: Guid.Empty);
                    break;

                case 2:
                    Validate(requestedSubmissionId: Guid.Empty);
                    break;

                case 3:
                    Validate(sessionSubmissionId: Guid.Empty);
                    break;
            }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingServerTimestamp_ShouldThrow(int field)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            switch (field)
            {
                case 0:
                    Validate(issuedAtUtc: default(DateTimeOffset));
                    break;

                case 1:
                    Validate(expiresAtUtc: default(DateTimeOffset));
                    break;

                case 2:
                    Validate(receivedAtUtc: default(DateTimeOffset));
                    break;
            }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NonUtcServerTimestamp_ShouldThrow(int field)
    {
        var nonUtcTime =
            IssuedAt.ToOffset(TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
        {
            switch (field)
            {
                case 0:
                    Validate(issuedAtUtc: nonUtcTime);
                    break;

                case 1:
                    Validate(expiresAtUtc: nonUtcTime);
                    break;

                case 2:
                    Validate(receivedAtUtc: nonUtcTime);
                    break;
            }
        });
    }

    [Fact]
    public void ExpiryEqualToIssueTime_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Validate(expiresAtUtc: IssuedAt));
    }

    [Fact]
    public void ExpiryBeforeIssueTime_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Validate(
                expiresAtUtc: IssuedAt.AddSeconds(-1)));
    }

    [Fact]
    public void WrongHunter_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(
                authenticatedHunterId: Guid.NewGuid()));
    }

    [Fact]
    public void WrongSubmission_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(
                requestedSubmissionId: Guid.NewGuid()));
    }

    [Fact]
    public void ConsumedSession_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(consumed: true));
    }

    [Fact]
    public void RequestBeforeSessionIssued_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(
                receivedAtUtc: IssuedAt.AddSeconds(-1)));
    }

    [Fact]
    public void RequestExactlyAtExpiry_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(receivedAtUtc: ExpiresAt));
    }

    [Fact]
    public void RequestAfterExpiry_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(
                receivedAtUtc: ExpiresAt.AddSeconds(1)));
    }
}
