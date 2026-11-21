using Wayhunter.Application.Modules.TripSafety.Memories;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3Ms07MediaEvidenceBoundaryTests
{
    private static readonly Guid OperationId = Guid.NewGuid();
    private static readonly Guid MemoryId = Guid.NewGuid();
    private static readonly string OwnerId = Guid.NewGuid().ToString("D");

    private static TripMemoryMedia Operation() =>
        new(
            OperationId,
            MemoryId,
            OwnerId,
            "image/png",
            1024,
            DateTimeOffset.UtcNow,
            TripMemoryMediaState.Pending);

    private static VerifiedMemoryAsset Asset() =>
        new(
            OperationId,
            MemoryId,
            OwnerId,
            "private/memory/object",
            "version-1",
            "image/png",
            1024,
            true,
            true);

    [Fact]
    public void MatchingPrivateImmutableAssetIsAccepted()
    {
        Assert.True(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset()));
    }

    [Fact]
    public void ForeignOperationIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    OperationId = Guid.NewGuid()
                }));
    }

    [Fact]
    public void ForeignMemoryIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    MemoryId = Guid.NewGuid()
                }));
    }

    [Fact]
    public void ForeignOwnerIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    OwnerUserId = Guid.NewGuid().ToString("D")
                }));
    }

    [Fact]
    public void PublicAssetIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    IsPrivate = false
                }));
    }

    [Fact]
    public void EmptyObjectKeyIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    ObjectKey = ""
                }));
    }

    [Fact]
    public void EmptyObjectVersionIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    ObjectVersion = ""
                }));
    }

    [Fact]
    public void OversizedObjectKeyIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    ObjectKey = new string('a', 451)
                }));
    }

    [Fact]
    public void OversizedObjectVersionIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    ObjectVersion = new string('v', 257)
                }));
    }

    [Fact]
    public void IncorrectContentLengthIsRejected()
    {
        Assert.False(
            TripMemoryMediaService.Matches(
                Operation(),
                Asset() with
                {
                    ContentLength = 2048
                }));
    }
}
