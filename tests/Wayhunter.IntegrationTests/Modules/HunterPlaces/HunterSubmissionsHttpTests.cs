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
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
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
            bool failStatusRead = false)
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
                Status = HunterSubmissionStatus.Draft,
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
}
