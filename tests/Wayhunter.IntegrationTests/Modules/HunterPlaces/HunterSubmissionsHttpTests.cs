using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Controllers.Mobile.HunterPlaces;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Services;
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
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(HunterSubmissionsController).Assembly);
            builder.Services.AddSwaggerGen();
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
