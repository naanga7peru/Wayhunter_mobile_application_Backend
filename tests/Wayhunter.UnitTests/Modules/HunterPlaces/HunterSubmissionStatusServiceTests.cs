using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterSubmissionStatusServiceTests
{
    private sealed class Reader(HunterSubmissionStatusSnapshot? snapshot)
        : IHunterSubmissionStatusReader
    {
        public int Calls { get; private set; }

        public Task<HunterSubmissionStatusSnapshot?> FindByIdAsync(
            Guid submissionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(snapshot);
        }
    }

    [Fact]
    public async Task OwnerGetsStatusProjectionWithoutOwnerIdentity()
    {
        var ownerId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow.AddMinutes(-2);
        var submitted = created.AddMinutes(1);
        var service = new HunterSubmissionStatusService(new Reader(
            new HunterSubmissionStatusSnapshot
            {
                SubmissionId = submissionId,
                OwnerUserId = ownerId.ToString("D"),
                PlaceName = "Old lighthouse",
                District = "Jaffna",
                Status = HunterSubmissionStatus.PendingReview,
                CreatedAtUtc = created,
                SubmittedAtUtc = submitted
            }));

        var result = await service.GetMySubmissionAsync(
            ownerId.ToString("D"), submissionId);

        Assert.NotNull(result);
        Assert.Equal(submissionId, result.SubmissionId);
        Assert.Equal("Old lighthouse", result.PlaceName);
        Assert.Equal(HunterSubmissionStatus.PendingReview, result.Status);
        Assert.DoesNotContain("OwnerUserId", result.GetType().GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task MissingAndForeignSubmissionsAreConcealed()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var reader = new Reader(new HunterSubmissionStatusSnapshot
        {
            SubmissionId = submissionId,
            OwnerUserId = ownerId.ToString("D")
        });
        var service = new HunterSubmissionStatusService(reader);

        Assert.Null(await service.GetMySubmissionAsync(
            otherId.ToString("D"), submissionId));
        Assert.Null(await service.GetMySubmissionAsync(
            ownerId.ToString("D"), Guid.NewGuid()));
        Assert.Equal(2, reader.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidActorIsRejectedBeforeStorage(string? actor)
    {
        var reader = new Reader(null);
        var service = new HunterSubmissionStatusService(reader);

        Assert.Null(await service.GetMySubmissionAsync(actor, Guid.NewGuid()));
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task CancellationIsObservedBeforeStorage()
    {
        var reader = new Reader(null);
        var service = new HunterSubmissionStatusService(reader);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetMySubmissionAsync(
                Guid.NewGuid().ToString("D"), Guid.NewGuid(), cts.Token));
        Assert.Equal(0, reader.Calls);
    }
}
