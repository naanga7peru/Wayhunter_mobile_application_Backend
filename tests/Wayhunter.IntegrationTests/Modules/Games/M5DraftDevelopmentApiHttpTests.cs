using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.Games;

public sealed class M5DraftDevelopmentApiHttpTests
{
    private readonly ITestOutputHelper _output;

    private const string SigningSecret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    public M5DraftDevelopmentApiHttpTests(ITestOutputHelper output) =>
        _output = output;

    [Fact]
    public async Task DevelopmentFlag_ExposesSecureOwnerScopedDraftCrud()
    {
        using var factory = new DraftApiFactory("Development", true);
        using var client = factory.CreateClient();

        using (var anonymous = await client.GetAsync(
                   $"/api/mobile/games/drafts/{Guid.NewGuid():D}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using var error = JsonDocument.Parse(
                await anonymous.Content.ReadAsStringAsync());
            Assert.Equal(
                "AUTHENTICATION_REQUIRED",
                error.RootElement.GetProperty("code").GetString());
            Assert.True(error.RootElement.TryGetProperty(
                "correlationId", out _));
        }

        var ownerToken = factory.CreateBearerToken(0);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);

        using var created = await CreateDraftAsync(client, TextDraft("First draft"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdBody = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());
        var gameId = createdBody.RootElement
            .GetProperty("gameId").GetGuid();
        Assert.Equal("Draft", createdBody.RootElement
            .GetProperty("status").GetString());
        Assert.False(createdBody.RootElement.TryGetProperty(
            "creatorUserId", out _));
        Assert.Equal(1, createdBody.RootElement
            .GetProperty("stages").GetArrayLength());

        using (var ownerRead = await client.GetAsync(
                   $"/api/mobile/games/drafts/{gameId:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, ownerRead.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", factory.CreateBearerToken(1));
        using (var crossOwner = await client.GetAsync(
                   $"/api/mobile/games/drafts/{gameId:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, crossOwner.StatusCode);
        }
        using (var crossOwnerUpdate = await client.PutAsJsonAsync(
                   $"/api/mobile/games/drafts/{gameId:D}",
                   TextDraft("Intrusion")))
        {
            Assert.Equal(HttpStatusCode.NotFound,
                crossOwnerUpdate.StatusCode);
        }
        using (var crossOwnerDelete = await client.DeleteAsync(
                   $"/api/mobile/games/drafts/{gameId:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound,
                crossOwnerDelete.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        using (var invalid = await CreateDraftAsync(client, TextDraft(" ")))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        using (var unverifiedMedia = await CreateDraftAsync(client, new CreateGameDraftDto(
                       "Media draft", null, 30,
                       [new GameDraftStageDto(
                           1, null, "not-verified-media")])))
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                unverifiedMedia.StatusCode);
            using var error = JsonDocument.Parse(
                await unverifiedMedia.Content.ReadAsStringAsync());
            Assert.Equal(
                "GAME_DRAFT_MEDIA_INVALID",
                error.RootElement.GetProperty("code").GetString());
        }

        using (var updated = await client.PutAsJsonAsync(
                   $"/api/mobile/games/drafts/{gameId:D}",
                   TextDraft("Updated draft")))
        {
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            using var body = JsonDocument.Parse(
                await updated.Content.ReadAsStringAsync());
            Assert.Equal("Updated draft", body.RootElement
                .GetProperty("title").GetString());
        }

        using (var deleted = await client.DeleteAsync(
                   $"/api/mobile/games/drafts/{gameId:D}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using (var repeatedDelete = await client.DeleteAsync(
                   $"/api/mobile/games/drafts/{gameId:D}"))
        {
            // Preserve the current application service's missing-resource
            // behavior; no idempotency contract is added here.
            Assert.Equal(HttpStatusCode.NotFound,
                repeatedDelete.StatusCode);
        }

        using var conflictCreated = await CreateDraftAsync(client, TextDraft("Submitted draft"));
        Assert.Equal(HttpStatusCode.Created, conflictCreated.StatusCode);
        using var conflictBody = JsonDocument.Parse(
            await conflictCreated.Content.ReadAsStringAsync());
        var submittedGameId = conflictBody.RootElement
            .GetProperty("gameId").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var submission = scope.ServiceProvider
                .GetRequiredService<GameSubmissionService>();
            await submission.SubmitAsync(
                submittedGameId,
                factory.Account(0),
                DateTimeOffset.UtcNow);
        }

        using (var nonDraftRead = await client.GetAsync(
                   $"/api/mobile/games/drafts/{submittedGameId:D}"))
        {
            Assert.Equal(HttpStatusCode.Conflict, nonDraftRead.StatusCode);
            using var error = JsonDocument.Parse(
                await nonDraftRead.Content.ReadAsStringAsync());
            Assert.Equal(
                "GAME_DRAFT_STATE_CONFLICT",
                error.RootElement.GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task DraftRoutes_AreUnmappedUnlessExplicitDevelopmentGate(
        string environment,
        bool enabled)
    {
        using var factory = new DraftApiFactory(environment, enabled);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/mobile/games/drafts/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SqlModeSwagger_ListsAuthenticatedDraftAndAdminReviewRoutes()
    {
        using var factory = new DraftApiFactory(
            "Development", enableDraftApi: true, enableSqlPersistence: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var operationCount = paths.EnumerateObject()
            .Sum(path => path.Value.EnumerateObject().Count());
        _output.WriteLine("Swagger operation count: {0}", operationCount);
        Assert.Equal(120, operationCount);
        Assert.True(paths.TryGetProperty("/api/mobile/home", out var home));
        Assert.True(home.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/mobile/games/drafts", out var drafts));
        Assert.True(drafts.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty(
            "/api/mobile/games/drafts/{gameId}", out var gameDraft));
        Assert.True(gameDraft.TryGetProperty("get", out _));
        Assert.True(gameDraft.TryGetProperty("put", out _));
        Assert.True(gameDraft.TryGetProperty("delete", out _));
        Assert.True(paths.TryGetProperty(
            "/api/mobile/games/drafts/{gameId}/submit", out _));
        Assert.True(paths.TryGetProperty(
            "/api/admin/games/reviews/pending", out _));
        Assert.True(paths.TryGetProperty(
            "/api/admin/games/reviews/{gameId}/approve", out _));
        Assert.True(paths.TryGetProperty(
            "/api/admin/games/reviews/{gameId}/reject", out _));
    }

    private static CreateGameDraftDto TextDraft(string title) => new(
        title,
        null,
        45,
        [new GameDraftStageDto(1, "Read the sign", null)]);

    private static async Task<HttpResponseMessage> CreateDraftAsync(
        HttpClient client,
        CreateGameDraftDto request)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post, "/api/mobile/games/drafts");
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        message.Content = JsonContent.Create(request);
        return await client.SendAsync(message);
    }

    private sealed class DraftApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _environment;
        private readonly bool _enableDraftApi;
        private readonly bool _enableSqlPersistence;
        private readonly UserAccount[] _accounts;
        private readonly IdentitySession[] _sessions;

        public DraftApiFactory(
            string environment,
            bool enableDraftApi,
            bool enableSqlPersistence = false)
        {
            _environment = environment;
            _enableDraftApi = enableDraftApi;
            _enableSqlPersistence = enableSqlPersistence;
            _accounts = CreateAccounts();
            _sessions = _accounts.Select(account =>
                IdentitySession.Create(
                    account.UserId,
                    account.VerifiedAtUtc!.Value.AddMinutes(1),
                    DateTimeOffset.UtcNow.AddHours(2))).ToArray();
        }

        public string CreateBearerToken(int accountIndex)
        {
            var tokens = Services.GetRequiredService<
                SignedSessionTokenService>();
            return tokens.Issue(
                _accounts[accountIndex],
                _sessions[accountIndex],
                DateTimeOffset.UtcNow);
        }

        public UserAccount Account(int index) => _accounts[index];

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["environment"] = _environment,
                        ["Identity:EnableSharedAuthentication"] = "true",
                        ["Games:EnableDevelopmentDraftHttpApi"] =
                            _enableDraftApi.ToString(),
                        ["Games:EnableSqlPersistence"] =
                            _enableSqlPersistence.ToString(),
                        ["ConnectionStrings:WayhunterSharedDb"] =
                            "Server=localhost;Database=DraftHttpTest;" +
                            "Trusted_Connection=True;TrustServerCertificate=True",
                        ["Authentication:Jwt:Issuer"] = "wayhunter-draft-test",
                        ["Authentication:Jwt:Audience"] = "wayhunter-mobile-test",
                        ["Authentication:Jwt:SigningSecret"] = SigningSecret
                    }));

            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticatedSessionReader>();
                services.AddSingleton<IAuthenticatedSessionReader>(
                    new TestSessionReader(_accounts, _sessions));
                services.RemoveAll<ITrustedIdentityAccountReader>();
                services.AddSingleton<ITrustedIdentityAccountReader>(
                    new TestAccountReader(_accounts));
            });
        }

        private static UserAccount[] CreateAccounts()
        {
            var now = DateTimeOffset.UtcNow.AddMinutes(-10);
            var accounts = Enumerable.Range(0, 2).Select(index =>
            {
                var account = UserAccount.RegisterPending(
                    $"Draft tester {index}",
                    $"draft-{Guid.NewGuid():N}@example.test",
                    "test-hash",
                    now);
                account.ActivateAfterVerifiedOtp(now.AddMinutes(1));
                return account;
            }).ToArray();
            return accounts;
        }
    }

    private sealed class TestAccountReader(UserAccount[] accounts)
        : ITrustedIdentityAccountReader
    {
        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(accounts.SingleOrDefault(
                account => account.UserId == userId));
        }
    }

    private sealed class TestSessionReader(
        UserAccount[] accounts,
        IdentitySession[] sessions) : IAuthenticatedSessionReader
    {
        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(accounts.SingleOrDefault(
                account => account.UserId == userId));
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(sessions.SingleOrDefault(
                session => session.SessionId == sessionId));
        }
    }
}
