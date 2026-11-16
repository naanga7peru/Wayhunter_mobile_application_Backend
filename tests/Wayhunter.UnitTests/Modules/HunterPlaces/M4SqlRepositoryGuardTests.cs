
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class M4SqlRepositoryGuardTests
{
    // No SQL database is accessed by these tests.
    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_M4_GuardOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static readonly DateTimeOffset NowUtc =
        new(2026, 11, 16, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_EmptyHunterId_ShouldReject()
    {
        using var db = CreateContext();
        var store = new SqlHunterCaptureSessionStore(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.CreateAsync(
                Guid.Empty,
                Guid.NewGuid(),
                NowUtc,
                NowUtc.AddMinutes(10)));
    }

    [Fact]
    public async Task CreateAsync_EmptySubmissionId_ShouldReject()
    {
        using var db = CreateContext();
        var store = new SqlHunterCaptureSessionStore(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.CreateAsync(
                Guid.NewGuid(),
                Guid.Empty,
                NowUtc,
                NowUtc.AddMinutes(10)));
    }

    [Fact]
    public async Task CreateAsync_InvalidTimeWindow_ShouldReject()
    {
        using var db = CreateContext();
        var store = new SqlHunterCaptureSessionStore(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                NowUtc,
                NowUtc));
    }

    [Fact]
    public async Task TryConsumeAsync_EmptySessionId_ShouldReturnFalse()
    {
        using var db = CreateContext();
        var store = new SqlHunterCaptureSessionStore(db);

        var result = await store.TryConsumeAsync(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            NowUtc);

        Assert.False(result);
    }

    [Fact]
    public async Task TryConsumeAsync_NonUtcTime_ShouldReturnFalse()
    {
        using var db = CreateContext();
        var store = new SqlHunterCaptureSessionStore(db);

        var nonUtc = NowUtc.ToOffset(
            TimeSpan.FromHours(5.5));

        var result = await store.TryConsumeAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            nonUtc);

        Assert.False(result);
    }

    [Fact]
    public async Task RevokeBySubmissionId_EmptyId_ShouldReject()
    {
        using var db = CreateContext();
        var store = new SqlHunterCaptureSessionStore(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.RevokeBySubmissionIdAsync(
                Guid.Empty));
    }

    [Fact]
    public async Task RevokeByHunterId_EmptyId_ShouldReject()
    {
        using var db = CreateContext();
        var store = new SqlHunterCaptureSessionStore(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.RevokeByHunterIdAsync(
                Guid.Empty));
    }

    [Fact]
    public async Task StatusTransition_UnsupportedTransition_ShouldReject()
    {
        using var db = CreateContext();

        var store =
            new SqlHunterSubmissionStatusTransitionStore(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store
                .TryTransitionAndRevokeCaptureSessionsAsync(
                    Guid.NewGuid(),
                    HunterSubmissionStatus.Draft,
                    HunterSubmissionStatus.Approved));
    }
}
