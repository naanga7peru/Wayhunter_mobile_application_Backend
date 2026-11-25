using System.Net.Http.Headers;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed partial class LocalDbAuthenticationPhase3Tests
{
    [LocalDbM4Fact]
    public Task VerifyM4Operations()
    {
        var catalog = Environment.GetEnvironmentVariable("WAYHUNTER_M4_FUNCTIONAL_CATALOG");
        Assert.True(catalog is not null && Regex.IsMatch(catalog, @"\AWayhunter_M4_Functional_[a-f0-9]{32}\z"));
        var media = new FixtureHunterMedia();
        var fault = new HunterCommitFault();
        bool simulate = Environment.GetEnvironmentVariable("WAYHUNTER_M4_SIMULATE_MEDIA") == "1";
        return RunIsolatedAsync(catalog!, migrateFreshCatalog: true,
            (client, db, capture, clock, _) =>
            {
                clock.Offset = TimeSpan.Zero;
                return VerifyHunterAsync(client, db, capture, simulate ? media : null, fault);
            },
            services =>
            {
                services.ConfigureDbContext<WayhunterSharedDbContext>(options => options.AddInterceptors(fault));
                if (!simulate) return;
                services.RemoveAll<IHunterMediaReceiptVerifier>();
                services.AddSingleton<IHunterMediaReceiptVerifier>(media);
            });
    }

    private async Task VerifyHunterAsync(HttpClient client, WayhunterSharedDbContext db, Capture capture,
        FixtureHunterMedia? media, HunterCommitFault fault)
    {
        const string mobile = "/api/mobile/hunter/submissions";
        const string adminRoute = "/api/admin/hunter/submissions";
        const string places = "/api/mobile/hunter/places";
        const string detail = mobile + "/{submissionId}";
        const string startCapture = detail + "/capture-sessions";
        const string bind = detail + "/evidence";
        const string submit = detail + "/submit";
        const string approve = adminRoute + "/{submissionId}/approve";
        const string reject = adminRoute + "/{submissionId}/reject";
        var sample = Guid.NewGuid().ToString("D");
        (string Method, string Route)[] operations =
        [
            ("GET", adminRoute), ("POST", approve), ("POST", reject), ("GET", places),
            ("GET", mobile), ("POST", mobile), ("DELETE", detail), ("GET", detail), ("PUT", detail),
            ("POST", startCapture), ("POST", bind), ("POST", submit)
        ];
        using (var swagger = await client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json"))
        {
            var paths = swagger!.RootElement.GetProperty("paths");
            foreach (var (method, route) in operations)
                Assert.True(paths.GetProperty(route).TryGetProperty(method.ToLowerInvariant(), out _), route);
        }
        foreach (var token in new string?[] { null, "invalid.jwt.token" })
        {
            Use(token);
            foreach (var (method, route) in operations)
                await Check(method, route, 401);
        }

        var owner = await Register("Owner");
        var other = await Register("Other");
        var admin = await Register("Admin");
        db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(admin.Id, DateTimeOffset.UtcNow, null));
        await db.SaveChangesAsync();
        Use(owner.Token);

        var draft = new HunterDraftRequestDto
        {
            PlaceName = "Fixture hunter place", Description = "Synthetic private description", ClaimedDistrict = "Jaffna",
            Latitude = 9.66, Longitude = 80.02, GpsAccuracyMeters = 5, LocationCapturedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)
        };
        var invalid = new HunterDraftRequestDto { PlaceName = "", Latitude = 91 };
        await Check("POST", mobile, 400, body: invalid, key: "invalid");
        await Check("POST", mobile, 400, body: draft);
        fault.FailNextCommit = true;
        await Check("POST", mobile, 503, body: draft, key: "first");
        Assert.False(fault.FailNextCommit);
        Assert.Empty(await db.HunterSubmissions.ToListAsync());
        Assert.Empty(await db.HunterRequestIdempotency.ToListAsync());
        // Exercise duplicate in-flight requests carrying the same operation key.
        var duplicate = await Task.WhenAll(
            Check("POST", mobile, 201, body: draft, key: "first"),
            Check("POST", mobile, 201, body: draft, key: "first"));
        var id = duplicate[0].GetProperty("submissionId").GetGuid();
        Assert.Equal(id, duplicate[1].GetProperty("submissionId").GetGuid());
        var path = $"{mobile}/{id}";
        Assert.Equal(1, await db.HunterSubmissions.CountAsync(x => x.UserId == owner.Id));
        Assert.Equal(1, await db.HunterRequestIdempotency.CountAsync(x => x.OwnerUserId == owner.Id));

        draft.PlaceName = "Updated hunter fixture";
        await Check("POST", mobile, 409, body: draft, key: "first");
        await Check("PUT", detail, 400, path, invalid);
        await Check("PUT", detail, 204, path, draft);
        var owned = await Check("GET", detail, 200, path);
        Assert.Equal(draft.PlaceName, owned.GetProperty("placeName").GetString());
        AssertSafe(owned);
        var ownedList = await Check("GET", mobile, 200);
        Assert.Single(ownedList.EnumerateArray());
        Assert.True(await db.HunterSubmissions.AnyAsync(x => x.Id == id && x.UserId == owner.Id &&
            x.PlaceName == draft.PlaceName && x.Status == HunterSubmissionStatus.Draft));

        Use(other.Token);
        Assert.Empty((await Check("GET", mobile, 200)).EnumerateArray());
        await Check("GET", detail, 404, path);
        await Check("PUT", detail, 404, path, draft);
        await Check("DELETE", detail, 404, path);
        await Check("POST", startCapture, 404, path + "/capture-sessions");
        await Check("POST", submit, 404, path + "/submit");
        await Check("GET", adminRoute, 403);
        await Check("POST", approve, 403, $"{adminRoute}/{id}/approve", new { });
        await Check("POST", reject, 403, $"{adminRoute}/{id}/reject", new { reason = "Synthetic review" });

        Use(owner.Token);
        var disposable = await Create("delete");
        await Check("DELETE", detail, 204, $"{mobile}/{disposable}");
        Assert.False(await db.HunterSubmissions.AnyAsync(x => x.Id == disposable));
        var video = await CaptureSession(id);
        Assert.True(await db.HunterCaptureSessions.AnyAsync(x => x.Id == video && x.OwnerUserId == owner.Id &&
            x.HunterSubmissionId == id && !x.IsConsumed && !x.IsRevoked));
        await Check("DELETE", detail, 409, path);
        await Check("POST", bind, 400, path + "/evidence", new { });
        await Check("POST", bind, 503, path + "/evidence",
            new HunterEvidenceBindRequestDto { CaptureSessionId = video, EvidenceType = HunterEvidenceType.Guided360Video, ProviderReceipt = "unknown-fixture" });
        if (media is not null)
            await Check("POST", bind, 503, path + "/evidence",
                new HunterEvidenceBindRequestDto { CaptureSessionId = video, EvidenceType = HunterEvidenceType.Guided360Video, ProviderReceipt = "fixture-timeout" });
        Assert.False(await db.HunterEvidence.AnyAsync(x => x.HunterSubmissionId == id));
        Assert.True(await db.HunterCaptureSessions.AnyAsync(x => x.Id == video && !x.IsConsumed));
        await Check("POST", submit, media is null ? 503 : 409, path + "/submit");

        Guid rejected = Guid.Empty;
        if (media is not null)
        {
            await BindAndSubmit(id, video);
            rejected = await Create("second");
            await BindAndSubmit(rejected, await CaptureSession(rejected));
        }
        else
        {
            // Independent review success is covered by the simulated provider profile.
            Assert.True(await db.HunterSubmissions.AnyAsync(x => x.Id == id && x.Status == HunterSubmissionStatus.Draft));
        }

        Use(admin.Token);
        var queue = await Check("GET", adminRoute, 200);
        foreach (var row in queue.EnumerateArray()) AssertSafe(row);
        await Check("POST", approve, 400, $"{adminRoute}/{Guid.Empty}/approve", new { });
        await Check("POST", reject, 400, $"{adminRoute}/{id}/reject", new { });
        await Check("POST", approve, 409, $"{adminRoute}/{sample}/approve", new { });
        await Check("POST", reject, 409, $"{adminRoute}/{sample}/reject", new { reason = "Synthetic review" });

        if (media is not null)
        {
            Assert.Equal(2, queue.GetArrayLength());
            Assert.Empty((await Check("GET", places, 200)).EnumerateArray());
            var outcomes = await Task.WhenAll(
                Send("POST", $"{adminRoute}/{id}/approve", new { }),
                Send("POST", $"{adminRoute}/{id}/approve", new { }));
            Assert.Equal(new[] { 200, 409 }, outcomes.Select(x => x.Status).Order());
            output.WriteLine($"POST {approve}: concurrent expected 200/409, actual {string.Join("/", outcomes.Select(x => x.Status).Order())}.");
            await Check("POST", reject, 200, $"{adminRoute}/{rejected}/reject", new { reason = "Synthetic decision" });
            await Check("POST", reject, 409, $"{adminRoute}/{rejected}/reject", new { reason = "Repeat" });
            Assert.True(await db.HunterSubmissions.AnyAsync(x => x.Id == id && x.Status == HunterSubmissionStatus.Approved));
            Assert.True(await db.HunterSubmissions.AnyAsync(x => x.Id == rejected && x.Status == HunterSubmissionStatus.Rejected));
            Assert.Equal(2, await db.HunterReviewDecisions.CountAsync(x => x.ReviewerUserId == admin.Id));
            Assert.Empty((await Check("GET", adminRoute, 200)).EnumerateArray());
        }

        Use(other.Token);
        var map = await Check("GET", places, 200);
        if (media is not null)
        {
            var approvedPlace = Assert.Single(map.EnumerateArray());
            Assert.Equal(id, approvedPlace.GetProperty("submissionId").GetGuid());
            AssertSafe(approvedPlace);
        }
        else Assert.Empty(map.EnumerateArray());

        void Use(string? token) => client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

        async Task<(int Status, JsonElement Body)> Send(string method, string route, object? body = null, string? key = null)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), route);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (key is not null) request.Headers.Add("Idempotency-Key", key);
            using var response = await client.SendAsync(request);
            var json = response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "application/json"
                ? await response.Content.ReadFromJsonAsync<JsonElement>() : default;
            return ((int)response.StatusCode, json);
        }

        async Task<JsonElement> Check(string method, string route, int expected, string? actualPath = null, object? body = null, string? key = null)
        {
            var response = await Send(method, actualPath ?? route.Replace("{submissionId}", sample), body, key);
            output.WriteLine($"{method} {route}: expected {expected}, actual {response.Status}.");
            Assert.Equal(expected, response.Status);
            return response.Body;
        }

        async Task<Guid> Create(string key) =>
            (await Check("POST", mobile, 201, body: draft, key: key)).GetProperty("submissionId").GetGuid();

        async Task<Guid> CaptureSession(Guid submission) =>
            (await Check("POST", startCapture, 201, $"{mobile}/{submission}/capture-sessions")).GetProperty("captureSessionId").GetGuid();

        async Task BindAndSubmit(Guid submission, Guid videoSession)
        {
            var target = $"{mobile}/{submission}";
            var photoSession = await CaptureSession(submission);
            var unused = await CaptureSession(submission);
            var photo = media!.Add(owner.Id, submission, photoSession, HunterEvidenceType.LivePhoto);
            await Check("POST", bind, 409, target + "/evidence", photo);
            var videoEvidence = media.Add(owner.Id, submission, videoSession, HunterEvidenceType.Guided360Video);
            Use(other.Token);
            await Check("POST", bind, 400, target + "/evidence", videoEvidence);
            Use(owner.Token);
            var bound = await Check("POST", bind, 201, target + "/evidence", videoEvidence);
            var replayed = await Check("POST", bind, 200, target + "/evidence", videoEvidence);
            Assert.Equal(bound.GetProperty("evidenceId").GetGuid(), replayed.GetProperty("evidenceId").GetGuid());
            AssertSafe(bound);
            photo = media.Add(owner.Id, submission, photoSession, HunterEvidenceType.LivePhoto);
            await Check("POST", bind, 201, target + "/evidence", photo);
            Assert.Equal(2, await db.HunterEvidence.CountAsync(x => x.HunterSubmissionId == submission &&
                x.OwnerUserId == owner.Id && x.ProviderVerifiedAtUtc != null));
            Assert.Equal(2, await db.HunterCaptureSessions.CountAsync(x => x.HunterSubmissionId == submission && x.IsConsumed));
            await Check("POST", submit, 200, target + "/submit");
            await Check("POST", submit, 200, target + "/submit");
            Assert.True(await db.HunterSubmissions.AnyAsync(x => x.Id == submission &&
                x.Status == HunterSubmissionStatus.PendingReview && x.SubmittedAtUtc != null));
            Assert.True(await db.HunterCaptureSessions.AnyAsync(x => x.Id == unused && x.IsRevoked));
        }

        async Task<(string Id, string Token)> Register(string name)
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            var password = Guid.NewGuid().ToString("N") + "A!";
            using var registration = await client.PostAsJsonAsync("/api/mobile/identity/register",
                new { displayName = name, email, password, confirmPassword = password });
            Assert.Equal(201, (int)registration.StatusCode);
            using var verified = await client.PostAsJsonAsync("/api/mobile/identity/verify-otp", new { email, code = capture.Codes.Last() });
            Assert.Equal(204, (int)verified.StatusCode);
            using var login = await client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
            Assert.Equal(200, (int)login.StatusCode);
            using var body = (await login.Content.ReadFromJsonAsync<JsonDocument>())!;
            return (body.RootElement.GetProperty("userId").GetString()!, body.RootElement.GetProperty("accessToken").GetString()!);
        }
    }

    private sealed class HunterCommitFault : DbTransactionInterceptor
    {
        public bool FailNextCommit { get; set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (FailNextCommit)
            {
                FailNextCommit = false;
                throw new InvalidOperationException("Synthetic failure before the draft transaction commit.");
            }
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }

    private static void AssertSafe(JsonElement dto) =>
        Assert.DoesNotContain(dto.EnumerateObject(), field => field.Name is "userId" or "userNameSnapshot" or
            "privateMediaReference" or "providerReceipt" or "providerReceiptDigest" or "providerAssetIdHash" or "reviewerUserId");

    private sealed class FixtureHunterMedia : IHunterMediaReceiptVerifier
    {
        private readonly Dictionary<string, VerifiedHunterMediaReceipt> _receipts = [];
        public bool IsConfigured => true;

        public HunterEvidenceBindRequestDto Add(string owner, Guid submission, Guid session, HunterEvidenceType type)
        {
            var key = Guid.NewGuid().ToString("N");
            _receipts.Add(key, new(owner, submission, session, "asset-" + key, "private/" + key, "receipt-" + key,
                type, DateTimeOffset.UtcNow, 9.66, 80.02, type == HunterEvidenceType.Guided360Video ? 20 : null));
            return new() { CaptureSessionId = session, EvidenceType = type, ProviderReceipt = key };
        }

        public Task<VerifiedHunterMediaReceipt?> VerifyAsync(string opaqueReceipt, string ownerUserId,
            Guid submissionId, Guid captureSessionId, CancellationToken cancellationToken = default) =>
            opaqueReceipt == "fixture-timeout"
                ? Task.FromException<VerifiedHunterMediaReceipt?>(new TimeoutException("Synthetic provider timeout."))
                : Task.FromResult(_receipts.GetValueOrDefault(opaqueReceipt));
    }
}

public sealed class LocalDbM4FactAttribute : FactAttribute
{
    public LocalDbM4FactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_LOCAL_M4_FUNCTIONAL") != "1")
            Skip = "Requires explicit M4 opt-in and a fresh dedicated LocalDB catalog.";
    }
}
