using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

public sealed partial class HunterHttpContractCandidateTests
{
    private static readonly Guid EvidenceSession = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Video = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Photo = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task EvidenceCandidates_RequireOwnedSessionAndRejectRepeatedCompletion()
    {
        using var factory = new CandidateFactory();
        using var client = factory.CreateClient();
        var path = Prefix + "/drafts/" + Draft + "/capture-sessions/" + EvidenceSession;
        var upload = new HunterEvidenceContractCandidateController.UploadRequest(
            HunterEvidenceType.Guided360Video, "video/mp4");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path + "/uploads", upload)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Subject", Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path + "/uploads", upload)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Subject");
        client.DefaultRequestHeaders.Add("X-Test-Subject", Owner.ToString("D"));
        var authorized = await client.PostAsJsonAsync(path + "/uploads", upload);
        Assert.Equal(HttpStatusCode.Created, authorized.StatusCode);
        var ticket = await authorized.Content.ReadFromJsonAsync<HunterUploadAuthorization>();
        Assert.NotNull(ticket);
        var request = new HunterEvidenceContractCandidateController.CompletionRequest(
            ticket.UploadId, HunterEvidenceType.Guided360Video);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/evidence",
            request with { EvidenceType = HunterEvidenceType.LivePhoto })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(path + "/evidence", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/evidence", request)).StatusCode);
    }

    [Fact]
    public async Task SubmitCandidate_RequiresVerifiedEvidenceAndReplaysCommittedResult()
    {
        using var factory = new CandidateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", Owner.ToString("D"));
        client.DefaultRequestHeaders.Add("Idempotency-Key", "test-submit");
        var path = Prefix + "/drafts/" + Draft + "/submit";
        var request = new HunterSubmitRequestDto
        {
            PlaceName = "Fort", ClaimedDistrict = "Jaffna", Latitude = 9, Longitude = 80,
            Guided360VideoEvidenceId = Video, LivePhotoEvidenceId = Guid.NewGuid()
        };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request)).StatusCode);
        request.LivePhotoEvidenceId = Photo;
        var first = await client.PostAsJsonAsync(path, request);
        var retry = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        request.PlaceName = "Conflicting change";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request)).StatusCode);
    }

    [Fact]
    public async Task AdminWriteCandidates_EnforcePermissionBindingAndValidation()
    {
        using var factory = new CandidateFactory();
        using var client = factory.CreateClient();
        var review = "/api/admin/v1/hunter/reviews/" + Draft;
        var place = "/api/admin/v1/hunter/places/" + Draft;
        var approve = new HunterReviewContractCandidateController.DecisionRequest("approve", null);
        var details = new HunterModerationContractCandidateController.Details("New name", null);
        var deactivate = new HunterModerationContractCandidateController.Deactivation("Test reason");
        client.DefaultRequestHeaders.Add("X-Test-Subject", Owner.ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(review + "/evidence/" + Video)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(review + "/decisions", approve)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(place + "/details", details)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(place + "/deactivate", deactivate)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Admin", "true");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(review + "/evidence/" + Photo)).StatusCode);
        var evidence = await client.GetAsync(review + "/evidence/" + Video);
        Assert.Equal(HttpStatusCode.OK, evidence.StatusCode);
        Assert.True(evidence.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(review + "/decisions",
            approve with { Decision = "reject" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(review + "/decisions", approve)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(review + "/decisions",
            approve with { Decision = "hold", Reason = "Review needed" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(place + "/details", details)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(place + "/deactivate", deactivate)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(place + "/details", details)).StatusCode);
    }

    private static void RegisterWriteCandidates(IServiceCollection services)
    {
        var reader = new InMemoryHunterCaptureSubmissionAccessReader(
            new[] { new HunterSubmission { Id = Draft, UserId = Owner.ToString("D") } });
        var ownership = new HunterCaptureSubmissionOwnershipService(reader);
        var identity = new HunterCaptureSessionIdentityResolutionService(new TestCaptureIdentity());
        var media = new TestEvidenceStore();
        services.AddSingleton(new HunterEvidenceWorkflowCandidate(ownership, identity,
            new HunterEvidenceReceiptCandidate(media), media, media, TimeProvider.System));
        var duplicates = new HunterDuplicateCandidateQueryService(new InMemoryHunterVerifiedPlaceReader(
            Array.Empty<ExistingVerifiedPlace>()));
        services.AddSingleton(new HunterSubmitWorkflowCandidate(ownership, identity, duplicates, new TestSubmitStore()));
        var admin = new TestAdminWrites();
        services.AddSingleton<IHunterReviewService>(admin);
        services.AddSingleton<IHunterPrivateEvidenceCandidate>(admin);
        services.AddSingleton<IHunterPlaceModerationCandidate>(admin);
    }

    private static HunterEvidence Proof(Guid id, HunterEvidenceType type, DateTimeOffset captured) => new()
    {
        Id = id, HunterSubmissionId = Draft, EvidenceType = type, PrivateMediaReference = "test-private",
        CapturedAtUtc = captured, UploadedAtUtc = captured.AddSeconds(1)
    };

    private sealed class TestEvidenceStore : IHunterEvidenceAtomicCandidateStore, IHunterUploadSignerCandidate,
        IHunterUploadReceiptVerifier
    {
        private readonly Guid upload = Guid.NewGuid();
        private bool issued;
        private int consumed;
        public Task<bool> CanAuthorizeAsync(HunterEvidenceBinding binding, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(binding.SessionId == EvidenceSession && binding.Type == HunterEvidenceType.Guided360Video &&
                Volatile.Read(ref consumed) == 0);

        public Task<HunterUploadAuthorization?> AuthorizeAsync(HunterEvidenceBinding binding, string type, CancellationToken ct)
        {
            issued = true;
            return Task.FromResult<HunterUploadAuthorization?>(
                new(upload, new Uri("https://media.example.test/upload"), DateTimeOffset.UtcNow.AddMinutes(1)));
        }

        public Task<HunterVerifiedUploadReceipt?> VerifyAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<HunterVerifiedUploadReceipt?>(id == upload && issued
                ? new(upload, Owner, Draft, EvidenceSession, "test-video",
                    Proof(Video, HunterEvidenceType.Guided360Video, DateTimeOffset.UtcNow.AddMinutes(-1))) : null);

        public Task<HunterEvidenceCompletion?> AssociateAsync(HunterEvidenceBinding binding,
            HunterVerifiedUploadReceipt receipt, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<HunterEvidenceCompletion?>(binding.SessionId == EvidenceSession &&
                Interlocked.Exchange(ref consumed, 1) == 0
                    ? new(Video, binding.Type, receipt.Evidence.CapturedAtUtc) : null);
    }

    private sealed class TestSubmitStore : IHunterSubmitAtomicCandidateStore
    {
        private readonly object gate = new();
        private HunterSubmissionResponseDto? committed;
        private string? committedOperation;

        private static string Operation(string owner, Guid id, string key, HunterSubmitRequestDto request) =>
            System.Text.Json.JsonSerializer.Serialize(new { owner, id, key, request });

        public Task<HunterSubmissionResponseDto?> FindReplayAsync(string owner, Guid id, string key,
            HunterSubmitRequestDto request, CancellationToken ct)
        {
            lock (gate)
            {
                if (committed is not null && committedOperation != Operation(owner, id, key, request))
                    throw new InvalidOperationException("Conflicting retry.");
                return Task.FromResult(committed);
            }
        }

        public Task<HunterVerifiedSubmitInputs?> ReadVerifiedInputsAsync(string owner, Guid id,
            HunterSubmitRequestDto request, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult<HunterVerifiedSubmitInputs?>(request.LivePhotoEvidenceId == Photo
                ? new("Test Hunter", "Jaffna", Proof(Video, HunterEvidenceType.Guided360Video, now.AddMinutes(-2)),
                    Proof(Photo, HunterEvidenceType.LivePhoto, now.AddMinutes(-1))) : null);
        }

        public Task<HunterSubmissionResponseDto?> CommitAsync(string owner, Guid id, string key,
            HunterSubmitRequestDto request, HunterVerifiedSubmitInputs inputs,
            IReadOnlyList<HunterDuplicateCandidate> candidates, CancellationToken ct)
        {
            lock (gate)
            {
                if (committed is not null && committedOperation != Operation(owner, id, key, request))
                    throw new InvalidOperationException("Conflicting retry.");
                committedOperation = Operation(owner, id, key, request);
                committed ??= new() { SubmissionId = id, Status = HunterSubmissionStatus.PendingReview };
                return Task.FromResult<HunterSubmissionResponseDto?>(committed);
            }
        }
    }

    private sealed class TestAdminWrites : IHunterReviewService, IHunterPrivateEvidenceCandidate,
        IHunterPlaceModerationCandidate
    {
        private readonly object gate = new();
        private HunterSubmissionStatus? decision;
        private bool active = true;

        public Task<HunterPrivateEvidenceAccess?> AuthorizeAsync(string reviewer, Guid id, Guid evidence, CancellationToken ct) =>
            Task.FromResult<HunterPrivateEvidenceAccess?>(id == Draft && evidence == Video
                ? new(new Uri("https://media.example.test/private"), DateTimeOffset.UtcNow.AddMinutes(1)) : null);

        public Task<HunterSubmissionResponseDto> ReviewAsync(string reviewer, HunterReviewRequestDto request,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                if (request.HunterSubmissionId != Draft || (decision.HasValue && decision != request.ResultingStatus))
                    throw new InvalidOperationException("Conflicting decision.");
                decision = request.ResultingStatus;
                return Task.FromResult(new HunterSubmissionResponseDto
                    { SubmissionId = Draft, Status = decision.Value });
            }
        }

        public Task<bool> UpdateAsync(string reviewer, Guid id, string name, string? description, CancellationToken ct)
        {
            lock (gate)
                return Task.FromResult(id == Draft && active);
        }

        public Task<bool> DeactivateAsync(string reviewer, Guid id, string reason, CancellationToken ct)
        {
            lock (gate)
            {
                if (id != Draft || !active)
                    return Task.FromResult(false);
                active = false;
                return Task.FromResult(true);
            }
        }
    }
}
