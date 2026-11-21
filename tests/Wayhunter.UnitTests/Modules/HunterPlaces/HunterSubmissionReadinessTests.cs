using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterSubmissionReadinessTests
{
    [Fact]
    public async Task OwnedReads_ReturnDraftAndStatusWithoutExposingAnotherOwnersSubmission()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new WayhunterSharedDbContext(options);
        var owner = Guid.NewGuid().ToString("D");
        var draft = new HunterSubmission { UserId = owner, PlaceName = "Draft place" };
        var foreign = new HunterSubmission { UserId = Guid.NewGuid().ToString("D") };
        db.HunterSubmissions.AddRange(draft, foreign);
        await db.SaveChangesAsync();

        var reader = new SqlHunterCaptureSubmissionAccessReader(db);

        Assert.Null(await reader.GetMySubmissionAsync(owner, foreign.Id));
        Assert.Null(await reader.GetMySubmissionAsync(owner, Guid.NewGuid()));
        var owned = Assert.Single(await reader.ListMySubmissionsAsync(owner.ToUpperInvariant()));
        Assert.Equal(draft.Id, owned.SubmissionId);
        Assert.Equal(HunterSubmissionStatus.Draft, owned.Status);
        var detail = await reader.GetMySubmissionAsync(owner, draft.Id);
        Assert.NotNull(detail);
        Assert.Equal(draft.PlaceName, detail.PlaceName);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => reader.ListMySubmissionsAsync("invalid"));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("wrong-id")]
    [InlineData("foreign-submission")]
    [InlineData("missing-photo")]
    [InlineData("wrong-order")]
    [InlineData("missing-media")]
    public void EvidenceReadiness_RequiresRequestedRecordsForTheSameSubmission(string scenario)
    {
        var submissionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var video = new HunterEvidence
        {
            HunterSubmissionId = submissionId,
            EvidenceType = HunterEvidenceType.Guided360Video,
            CapturedAtUtc = now.AddMinutes(-3),
            UploadedAtUtc = now.AddMinutes(-2),
            PrivateMediaReference = "test/video"
        };
        var photo = new HunterEvidence
        {
            HunterSubmissionId = scenario == "foreign-submission" ? Guid.NewGuid() : submissionId,
            EvidenceType = HunterEvidenceType.LivePhoto,
            CapturedAtUtc = scenario == "wrong-order" ? now.AddMinutes(-3) : now.AddMinutes(-1),
            UploadedAtUtc = now,
            PrivateMediaReference = scenario == "missing-media" ? "" : "test/photo"
        };
        var request = new HunterSubmitRequestDto
        {
            Guided360VideoEvidenceId = scenario == "wrong-id" ? Guid.NewGuid() : video.Id,
            LivePhotoEvidenceId = photo.Id
        };

        var exception = Record.Exception(() => HunterSubmissionEvidenceValidator.EnsureReady(
            submissionId, request, video, scenario == "missing-photo" ? null : photo));

        if (scenario == "valid")
            Assert.Null(exception);
        else if (scenario == "missing-media")
            Assert.IsType<ArgumentException>(exception);
        else
            Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task DraftCreation_UsesTrustedOwnerAndVerifiedLocation()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new WayhunterSharedDbContext(options);
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.RegisterPending("Hunter", "hunter@example.com", "test-hash", now.AddMinutes(-2));
        account.ActivateAfterVerifiedOtp(now.AddMinutes(-1));
        var reader = new SqlHunterCaptureSubmissionAccessReader(db);
        var service = new HunterDraftCreationService(new DraftAccountReader(account), reader, TimeProvider.System);
        var location = new HunterVerifiedDraftLocation("Jaffna", 9.7, 80, 10, now);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(Guid.NewGuid().ToString("D"), "Place", null, location));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(account.UserId, "Place", null, location with { Latitude = double.NaN }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateDetailsAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), "Place", null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateDetailsAsync(account.UserId, Guid.Empty, "Place", null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateDetailsAsync(account.UserId, Guid.NewGuid(), " ", null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.UpdateDetailsAsync(account.UserId, Guid.NewGuid(), "Place", new string('a', 2001)));
        Assert.Empty(db.HunterSubmissions);

        var response = await service.CreateAsync(account.UserId, " Place ", null, location);
        var stored = await db.HunterSubmissions.SingleAsync();
        Assert.Equal(stored.Id, response.SubmissionId);
        Assert.Equal(account.UserId, stored.UserId);
        Assert.Equal(account.DisplayName, stored.UserNameSnapshot);
        Assert.Equal("Place", stored.PlaceName);
        Assert.Equal(location.District, stored.District);
        Assert.Equal(HunterSubmissionStatus.Draft, stored.Status);
        Assert.Null(stored.SubmittedAtUtc);
        Assert.Equal(0, await reader.CountMyApprovedHuntsAsync(account.UserId));

        stored.Status = HunterSubmissionStatus.Approved;
        await db.SaveChangesAsync();
        Assert.Equal(1, await reader.CountMyApprovedHuntsAsync(account.UserId));
        Assert.Equal(0, await reader.CountMyApprovedHuntsAsync(Guid.NewGuid().ToString("D")));
    }

    private sealed class DraftAccountReader(UserAccount account) : ITrustedIdentityAccountReader
    {
        public Task<UserAccount?> FindByUserIdAsync(
            string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(account);
    }
}
