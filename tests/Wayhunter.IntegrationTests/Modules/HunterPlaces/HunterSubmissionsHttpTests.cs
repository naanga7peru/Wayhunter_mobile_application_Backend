using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Controllers.Mobile.HunterPlaces;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

public sealed class HunterSubmissionsHttpTests
{
    private const string SigningSecret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private sealed class TestSessionReader(
        UserAccount account,
        IdentitySession session) : IAuthenticatedSessionReader
    {
        public Task<UserAccount?> FindAccountAsync(
            string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(account.UserId == userId ? account : null);

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IdentitySession?>(session.SessionId == sessionId ? session : null);
    }

    private sealed class TrustedAccountReader(UserAccount? account)
        : ITrustedIdentityAccountReader
    {
        public Task<UserAccount?> FindByUserIdAsync(
            string userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<UserAccount?>(
                account?.UserId == userId ? account : null);
        }
    }

    private sealed class FailingStatusReader : IHunterSubmissionStatusReader
    {
        public Task<HunterSubmissionStatusSnapshot?> FindByIdAsync(
            Guid submissionId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Sensitive database exception details.");

        public Task<IReadOnlyList<HunterSubmissionStatusSnapshot>> FindMineAsync(
            string ownerUserId, bool approvedOnly = false,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Sensitive database exception details.");

        public Task<IReadOnlyList<ApprovedHunterPlaceDto>> FindApprovedPlacesAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Sensitive database exception details.");

        public Task<IReadOnlyList<HunterSubmissionStatusSnapshot>> FindPendingReviewAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Sensitive database exception details.");
    }

    private sealed class FailingCaptureSessionStore : IHunterCaptureSessionStore
    {
        public Task<Guid> CreateAsync(Guid hunterId, Guid hunterSubmissionId,
            DateTimeOffset issuedAtUtc, DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Capture provider unavailable.");

        public Task<bool> TryConsumeAsync(Guid captureSessionId, Guid authenticatedHunterId,
            Guid hunterSubmissionId, DateTimeOffset serverReceivedAtUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class DraftStore : IHunterDraftStore
    {
        public HunterSubmission? Created { get; private set; }
        public Task<HunterSubmission> CreateAsync(HunterSubmission submission, string keyHash, string requestDigest,
            CancellationToken cancellationToken = default)
        { Created = submission; return Task.FromResult(submission); }
        public Task<HunterDraftUpdateResult> UpdateDraftAsync(Guid submissionId, string ownerUserId,
            HunterDraftRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult(HunterDraftUpdateResult.Updated);
        public Task<HunterDraftDeleteResult> DeleteDraftAsync(Guid submissionId, string ownerUserId,
            CancellationToken cancellationToken = default) => Task.FromResult(HunterDraftDeleteResult.Deleted);
    }

    private sealed class EvidenceStore : IHunterEvidenceStore
    {
        public Task<HunterEvidenceBindResult> BindAsync(Guid submissionId, Guid sessionId, string ownerUserId,
            Guid hunterId, VerifiedHunterMediaReceipt receipt, DateTimeOffset receivedAtUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(new HunterEvidenceBindResult(false, false, Guid.Empty));
        public Task<bool> HasCompleteVerifiedEvidenceAsync(Guid submissionId, string ownerUserId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class TransitionStore : IHunterSubmissionStatusTransitionStore
    {
        public Task<bool> TrySubmitDraftAndRevokeCaptureSessionsAsync(Guid submissionId, DateTimeOffset submittedAtUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> TryTransitionAndRevokeCaptureSessionsAsync(Guid submissionId,
            HunterSubmissionStatus expectedStatus, HunterSubmissionStatus nextStatus,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private const string Route = "/api/mobile/hunter/submissions/";
        private readonly WebApplication _app;
        private readonly HttpClient _client;

        public UserAccount Owner { get; }
        public Guid SubmissionId { get; } = Guid.NewGuid();
        public Guid ForeignSubmissionId { get; } = Guid.NewGuid();

        public Fixture(
            bool invalidateSessionAccount = false,
            bool failStatusRead = false,
            bool failCapture = false,
            HunterSubmissionStatus submissionStatus = HunterSubmissionStatus.Draft)
        {
            var now = DateTimeOffset.UtcNow;
            Owner = UserAccount.RegisterPending(
                "M4 HTTP Owner",
                $"hunter-{Guid.NewGuid():N}@example.test",
                "test-only-password-hash",
                now.AddMinutes(-10));
            Owner.ActivateAfterVerifiedOtp(now.AddMinutes(-9));
            var session = IdentitySession.Create(
                Owner.UserId, now.AddMinutes(-1), now.AddMinutes(20));
            var database = Guid.NewGuid().ToString("N");

            var builder = WebApplication.CreateBuilder(
                new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Configuration["Authentication:Jwt:Issuer"] = "m4-http-test";
            builder.Configuration["Authentication:Jwt:Audience"] = "mobile-test";
            builder.Configuration["Authentication:Jwt:SigningSecret"] = SigningSecret;
            builder.Configuration["Identity:AdministratorRole"] = "Admin";
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(HunterSubmissionsController).Assembly);
            builder.Services.AddSwaggerGen();
            builder.Services.AddCoinsWalletAdministratorPolicy(builder.Configuration);
            builder.Services.AddDbContext<WayhunterSharedDbContext>(options =>
                options.UseInMemoryDatabase(database));
            builder.Services.AddSingleton<IAuthenticatedSessionReader>(
                new TestSessionReader(Owner, session));
            builder.Services.AddSingleton<ITrustedIdentityAccountReader>(
                new TrustedAccountReader(Owner));
            builder.Services.AddScoped<IHunterSubmissionStatusReader>(provider =>
                failStatusRead
                    ? new FailingStatusReader()
                    : new SqlHunterSubmissionStatusReader(
                        provider.GetRequiredService<WayhunterSharedDbContext>()));
            builder.Services.AddScoped<
                Wayhunter.Application.Modules.HunterPlaces.Services.HunterSubmissionStatusService>();
            var draftStore = new DraftStore();
            builder.Services.AddSingleton<IHunterDraftStore>(draftStore);
            builder.Services.AddScoped<HunterDraftService>();
            builder.Services.AddSingleton<IHunterEvidenceStore, EvidenceStore>();
            builder.Services.AddSingleton<Wayhunter.Application.Modules.HunterPlaces.Interfaces.IHunterMediaReceiptVerifier,
                Wayhunter.Infrastructure.Integrations.HunterPlaces.DisabledHunterMediaReceiptVerifier>();
            builder.Services.AddScoped<HunterEvidenceService>();
            builder.Services.AddSingleton<IHunterSubmissionStatusTransitionStore, TransitionStore>();
            builder.Services.AddScoped<HunterSubmissionStatusTransitionService>();
            builder.Services.AddScoped<IHunterCaptureSubmissionAccessReader,
                SqlHunterCaptureSubmissionAccessReader>();
            if (failCapture)
                builder.Services.AddScoped<IHunterCaptureSessionStore, FailingCaptureSessionStore>();
            else
                builder.Services.AddSingleton<IHunterCaptureSessionStore,
                    InMemoryHunterCaptureSessionStore>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<HunterCaptureSubmissionOwnershipService>();
            builder.Services.AddScoped<HunterCaptureSessionIdentityResolutionService>();
            builder.Services.AddScoped<HunterCaptureSessionService>();
            builder.Services.AddScoped<HunterStartCaptureSessionHandler>();
            builder.Services.AddScoped<HunterStartAuthorizedCaptureSessionHandler>();
            builder.Services.AddScoped<IHunterCaptureSessionIdentityResolver,
                TrustedHunterCaptureSessionIdentityResolver>();
            builder.Services.AddWayhunterSessionAuthentication(builder.Configuration);

            _app = builder.Build();
            _app.UseMiddleware<Wayhunter.Api.Middleware.CorrelationIdMiddleware>();
            _app.UseAuthentication();
            _app.UseAuthorization();
            _app.MapControllers();
            _app.StartAsync().GetAwaiter().GetResult();

            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WayhunterSharedDbContext>();
            var submission = new HunterSubmission
            {
                Id = SubmissionId,
                UserId = Owner.UserId,
                UserNameSnapshot = "Private owner snapshot",
                PlaceName = "Public-facing place",
                Description = "Private description excluded from response",
                District = "Jaffna",
                Latitude = 9.66845,
                Longitude = 80.0074,
                Status = submissionStatus,
                CreatedAtUtc = now
            };
            db.Users.Add(Owner);
            db.HunterSubmissions.Add(submission);
            db.HunterSubmissions.Add(new HunterSubmission
            {
                Id = ForeignSubmissionId,
                UserId = Guid.NewGuid().ToString("D"),
                UserNameSnapshot = "Another private owner",
                PlaceName = "Foreign place",
                Description = "Must not be returned",
                District = "Colombo",
                Latitude = 6.9271,
                Longitude = 79.8612,
                Status = HunterSubmissionStatus.PendingReview,
                CreatedAtUtc = now
            });
            if (submissionStatus == HunterSubmissionStatus.Approved)
            {
                db.HunterReviewDecisions.Add(new HunterReviewDecision
                {
                    HunterSubmissionId = SubmissionId,
                    ReviewerUserId = Owner.UserId,
                    ResultingStatus = HunterSubmissionStatus.Approved,
                    ReviewedAtUtc = now.AddMinutes(1)
                });
            }
            db.SaveChanges();

            var tokens = new SignedSessionTokenService(
                "m4-http-test", "mobile-test", SigningSecret);
            var client = _app.GetTestClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", tokens.Issue(Owner, session, now));
            if (invalidateSessionAccount)
            {
                // Token was issued while the account was eligible; the
                // persisted account is now suspended before the request.
                Owner.Suspend();
            }
            _client = client;
        }

        public HttpClient Client => _client;
        public string Url(Guid? id = null) => Route + (id ?? SubmissionId);
        public IEnumerable<string> OpenApiPaths => _app.Services
            .GetRequiredService<ISwaggerProvider>()
            .GetSwagger("v1")
            .Paths.Keys;

        public bool HasOpenApiPost(string path)
        {
            var paths = _app.Services
                .GetRequiredService<ISwaggerProvider>()
                .GetSwagger("v1")
                .Paths;
            return paths.TryGetValue(path, out var pathItem) &&
                pathItem?.Operations?.Keys.Any(operation =>
                    string.Equals(operation.ToString(), "Post", StringComparison.OrdinalIgnoreCase)) == true;
        }

    public int HunterOpenApiOperationCount => _app.Services
            .GetRequiredService<ISwaggerProvider>().GetSwagger("v1")
            .Paths.Where(x => x.Key.Contains("/hunter/", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Value is { Operations: { } operations } ? operations.Count : 0);

        public T GetService<T>() where T : notnull =>
            _app.Services.GetRequiredService<T>();

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Owner_ReturnsOnlyApprovedSubmissionProjection()
    {
        await using var fixture = new Fixture();
        var response = await fixture.Client.GetAsync(fixture.Url());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<HunterSubmissionStatusResponse>();
        Assert.NotNull(dto);
        Assert.Equal("Public-facing place", dto.PlaceName);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("owner", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("submissionId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latitude", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovedGetRoute_IsPublishedToOpenApiDiscovery()
    {
        await using var fixture = new Fixture();

        Assert.Contains(
            "/api/mobile/hunter/submissions/{submissionId}",
            fixture.OpenApiPaths);
    }

    [Fact]
    public async Task ApprovedPostRoute_IsPublishedToOpenApiDiscovery()
    {
        await using var fixture = new Fixture();

        const string path = "/api/mobile/hunter/submissions/{submissionId}/capture-sessions";
        Assert.Contains(path, fixture.OpenApiPaths);
        Assert.True(fixture.HasOpenApiPost(path));
    }

    [Fact]
    public async Task M4OpenApiDocumentsAllTwelveOperations()
    {
        await using var fixture = new Fixture();
        var paths = fixture.OpenApiPaths.ToArray();
        Assert.Contains("/api/mobile/hunter/submissions", paths);
        Assert.Contains("/api/mobile/hunter/submissions/{submissionId}/evidence", paths);
        Assert.Contains("/api/mobile/hunter/submissions/{submissionId}/submit", paths);
        Assert.Contains("/api/admin/hunter/submissions/{submissionId}/approve", paths);
        Assert.Contains("/api/admin/hunter/submissions/{submissionId}/reject", paths);
        Assert.Equal(12, fixture.HunterOpenApiOperationCount);
    }

    [Fact]
    public async Task AdminHunterQueue_RequiresCentralAdminRole()
    {
        await using var fixture = new Fixture();
        using var regularUserResponse = await fixture.Client.GetAsync("/api/admin/hunter/submissions");
        Assert.Equal(HttpStatusCode.Forbidden, regularUserResponse.StatusCode);
        using var regularApprove = await fixture.Client.PostAsync(
            $"/api/admin/hunter/submissions/{fixture.SubmissionId:D}/approve",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, regularApprove.StatusCode);

        fixture.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymousResponse = await fixture.Client.GetAsync("/api/admin/hunter/submissions");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        using var anonymousReject = await fixture.Client.PostAsync(
            $"/api/admin/hunter/submissions/{fixture.SubmissionId:D}/reject",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReject.StatusCode);
    }

    [Fact]
    public async Task DraftCreateRequiresIdempotencyKeyAndPersistsTrustedOwner()
    {
        await using var fixture = new Fixture();
        var body = new HunterDraftRequestDto
        {
            PlaceName = "Test waterfall", ClaimedDistrict = "Jaffna",
            Latitude = 9.66, Longitude = 80.00
        };
        var missingKey = await fixture.Client.PostAsJsonAsync(
            "/api/mobile/hunter/submissions", body);
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);

        fixture.Client.DefaultRequestHeaders.Add("Idempotency-Key", "draft-create-1");
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/mobile/hunter/submissions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HunterSubmissionResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(HunterSubmissionStatus.Draft, result.Status);
        var stored = Assert.IsType<DraftStore>(fixture.GetService<IHunterDraftStore>()).Created;
        Assert.NotNull(stored);
        Assert.Equal(fixture.Owner.UserId, stored.UserId);
        Assert.Equal("M4 HTTP Owner", stored.UserNameSnapshot);
        Assert.NotEqual(Guid.Empty, stored.Id);
    }

    [Fact]
    public async Task EvidenceBindRemainsUnavailableWithoutTrustedProvider()
    {
        await using var fixture = new Fixture();
        var response = await fixture.Client.PostAsJsonAsync(
            fixture.Url() + "/evidence", new HunterEvidenceBindRequestDto
            {
                CaptureSessionId = Guid.NewGuid(),
                EvidenceType = HunterEvidenceType.Guided360Video,
                ProviderReceipt = "unverified client input"
            });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("hunter_media_unavailable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SubmissionRemainsUnavailableUntilTrustedEvidenceProviderExists()
    {
        await using var fixture = new Fixture();
        var response = await fixture.Client.PostAsync(fixture.Url() + "/submit", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("hunter_media_unavailable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnonymousCaller_Returns401()
    {
        await using var fixture = new Fixture();
        fixture.Client.DefaultRequestHeaders.Authorization = null;

        var response = await fixture.Client.GetAsync(fixture.Url());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<ApiErrorResponse>());
    }

    [Fact]
    public async Task MissingAndForeignSubmissions_ReturnIdentical404Responses()
    {
        await using var fixture = new Fixture();
        var missingResponse = await fixture.Client.GetAsync(fixture.Url(Guid.NewGuid()));
        var foreignResponse = await fixture.Client.GetAsync(fixture.Url(fixture.ForeignSubmissionId));

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        var missingError = await missingResponse.Content
            .ReadFromJsonAsync<ApiErrorResponse>();
        var foreignError = await foreignResponse.Content
            .ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(missingError);
        Assert.NotNull(foreignError);
        Assert.Equal(missingError.Code, foreignError.Code);
        Assert.Equal(missingError.Message, foreignError.Message);
        Assert.NotEqual(missingError.CorrelationId, foreignError.CorrelationId);
    }

    [Fact]
    public async Task MySubmissionsAndApprovedPlaces_AreOwnerScoped()
    {
        await using var fixture = new Fixture(submissionStatus: HunterSubmissionStatus.Approved);
        using var submissions = await fixture.Client.GetAsync("/api/mobile/hunter/submissions");
        Assert.Equal(HttpStatusCode.OK, submissions.StatusCode);
        using var rows = JsonDocument.Parse(await submissions.Content.ReadAsStringAsync());
        Assert.Single(rows.RootElement.EnumerateArray());
        Assert.Equal(fixture.SubmissionId, rows.RootElement[0].GetProperty("submissionId").GetGuid());
        Assert.False(rows.RootElement[0].TryGetProperty("ownerUserId", out _));

        using var places = await fixture.Client.GetAsync("/api/mobile/hunter/places");
        Assert.Equal(HttpStatusCode.OK, places.StatusCode);
        using var approved = JsonDocument.Parse(await places.Content.ReadAsStringAsync());
        Assert.Single(approved.RootElement.EnumerateArray());
        Assert.Equal("Public-facing place", approved.RootElement[0].GetProperty("placeName").GetString());
        Assert.True(approved.RootElement[0].TryGetProperty("latitude", out _));
    }

    [Fact]
    public async Task InactiveTrustedAccount_IsDenied()
    {
        await using var fixture = new Fixture(invalidateSessionAccount: true);

        var response = await fixture.Client.GetAsync(fixture.Url());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("M4 HTTP Owner",
            await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusDependencyFailure_ReturnsSafe503()
    {
        await using var fixture = new Fixture(failStatusRead: true);

        var response = await fixture.Client.GetAsync(fixture.Url());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("hunter_submission_unavailable", error.Code);
        Assert.DoesNotContain("Sensitive database", error.Message);
        Assert.DoesNotContain("Sensitive database",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Owner_CreatesCaptureSessionAndReturns201()
    {
        await using var fixture = new Fixture();

        var response = await fixture.Client.PostAsync(
            fixture.Url() + "/capture-sessions", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<HunterStartCaptureSessionResponseDto>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.CaptureSessionId);
        Assert.Equal(fixture.SubmissionId, result.HunterSubmissionId);
    }

    [Fact]
    public async Task AnonymousCaptureCaller_Returns401()
    {
        await using var fixture = new Fixture();
        fixture.Client.DefaultRequestHeaders.Authorization = null;

        var response = await fixture.Client.PostAsync(
            fixture.Url() + "/capture-sessions", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingAndForeignCaptureSubmissions_ReturnIdentical404()
    {
        await using var fixture = new Fixture();
        var missing = await fixture.Client.PostAsync(
            fixture.Url(Guid.NewGuid()) + "/capture-sessions", null);
        var foreign = await fixture.Client.PostAsync(
            fixture.Url(fixture.ForeignSubmissionId) + "/capture-sessions", null);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var missingError = await missing.Content.ReadFromJsonAsync<ApiErrorResponse>();
        var foreignError = await foreign.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(missingError?.Code, foreignError?.Code);
        Assert.Equal(missingError?.Message, foreignError?.Message);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    [InlineData(HunterSubmissionStatus.Held)]
    [InlineData(HunterSubmissionStatus.Approved)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task NonDraftOwnedSubmission_Returns409(
        HunterSubmissionStatus submissionStatus)
    {
        await using var fixture = new Fixture(submissionStatus: submissionStatus);

        var response = await fixture.Client.PostAsync(
            fixture.Url() + "/capture-sessions", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnavailableCaptureProvider_ReturnsSafe503()
    {
        await using var fixture = new Fixture(failCapture: true);

        var response = await fixture.Client.PostAsync(
            fixture.Url() + "/capture-sessions", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("hunter_capture_unavailable", error?.Code);
        Assert.DoesNotContain("provider", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InactiveTrustedAccount_IsDeniedForCaptureSession()
    {
        await using var fixture = new Fixture(invalidateSessionAccount: true);

        var response = await fixture.Client.PostAsync(
            fixture.Url() + "/capture-sessions", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatedSessionRetainsTenMinuteExpiryAndSingleUse()
    {
        await using var fixture = new Fixture();
        var response = await fixture.Client.PostAsync(
            fixture.Url() + "/capture-sessions", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content
            .ReadFromJsonAsync<HunterStartCaptureSessionResponseDto>();
        Assert.NotNull(session);

        var store = fixture.GetService<IHunterCaptureSessionStore>();
        var hunterId = Guid.Parse(fixture.Owner.UserId);
        var acceptedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        Assert.True(await store.TryConsumeAsync(
            session.CaptureSessionId, hunterId, fixture.SubmissionId, acceptedAt));
        Assert.False(await store.TryConsumeAsync(
            session.CaptureSessionId, hunterId, fixture.SubmissionId, acceptedAt));

        var expiredResponse = await fixture.Client.PostAsync(
            fixture.Url() + "/capture-sessions", null);
        var expiredSession = await expiredResponse.Content
            .ReadFromJsonAsync<HunterStartCaptureSessionResponseDto>();
        Assert.NotNull(expiredSession);
        Assert.False(await store.TryConsumeAsync(
            expiredSession.CaptureSessionId, hunterId, fixture.SubmissionId,
            DateTimeOffset.UtcNow.AddMinutes(11)));
    }
}
