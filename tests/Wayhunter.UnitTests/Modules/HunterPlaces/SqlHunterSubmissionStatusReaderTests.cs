using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class SqlHunterSubmissionStatusReaderTests
{
    [Fact]
    public async Task ReaderProjectsStatusAndOwnerWithoutTracking()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var ownerId = Guid.NewGuid().ToString("D");
        var submission = new HunterSubmission
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            UserNameSnapshot = "Test Hunter",
            PlaceName = "Test Place",
            District = "Jaffna",
            Latitude = 9.66,
            Longitude = 80.00,
            Status = HunterSubmissionStatus.PendingReview,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        await using var db = new WayhunterSharedDbContext(options);
        db.HunterSubmissions.Add(submission);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var reader = new SqlHunterSubmissionStatusReader(db);

        var snapshot = await reader.FindByIdAsync(submission.Id);

        Assert.NotNull(snapshot);
        Assert.Equal(ownerId, snapshot.OwnerUserId);
        Assert.Equal(submission.PlaceName, snapshot.PlaceName);
        Assert.Equal(submission.Status, snapshot.Status);
        Assert.Equal(EntityState.Detached, db.Entry(submission).State);
    }

    [Fact]
    public async Task EmptySubmissionIdReturnsNull()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new WayhunterSharedDbContext(options);

        var reader = new SqlHunterSubmissionStatusReader(db);

        Assert.Null(await reader.FindByIdAsync(Guid.Empty));
    }
}
