using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Api.Services.Games;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.Games;

public sealed class GamesReadApiHttpTests
{
    [Fact]
    public async Task AuthorizedUser_CanReadListDetailsAndOwnAttempts()
    {
        using var factory = new GamesReadFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(0));

        using var list = await client.GetAsync("/api/mobile/games");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using (var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            Assert.Equal(factory.PublicGameId, body.RootElement[0].GetProperty("gameId").GetGuid());
            Assert.False(body.RootElement[0].TryGetProperty("creatorUserId", out _));
        }

        using var detail = await client.GetAsync($"/api/mobile/games/{factory.PublicGameId:D}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using (var body = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal(3, body.RootElement.GetProperty("stageCount").GetInt32());
            Assert.False(body.RootElement.TryGetProperty("creatorUserId", out _));
            Assert.False(body.RootElement.TryGetProperty("photoMediaId", out _));
        }

        using var attempts = await client.GetAsync("/api/mobile/games/me/attempts");
        Assert.Equal(HttpStatusCode.OK, attempts.StatusCode);
        using (var body = JsonDocument.Parse(await attempts.Content.ReadAsStringAsync()))
        {
            Assert.Single(body.RootElement.EnumerateArray());
            Assert.False(body.RootElement[0].TryGetProperty("playerUserId", out _));
            Assert.False(body.RootElement[0].TryGetProperty("evidence", out _));
        }

        Assert.Equal(factory.Account(0).UserId, factory.Attempts.LastRequestedUserId);
    }

    [Fact]
    public async Task Attempts_NeverReturnAnotherPlayersRows()
    {
        using var factory = new GamesReadFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(1));

        using var response = await client.GetAsync("/api/mobile/games/me/attempts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.EnumerateArray());
        Assert.Equal(factory.Account(1).UserId, factory.Attempts.LastRequestedUserId);
    }

    [Fact]
    public async Task AnonymousAndInactiveAccounts_AreRejected()
    {
        using var factory = new GamesReadFactory();
        using var client = factory.CreateClient();

        using (var anonymous = await client.GetAsync("/api/mobile/games"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(2));
        using var inactive = await client.GetAsync("/api/mobile/games/me/attempts");
        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-identity");
        using var invalidIdentity = await client.GetAsync("/api/mobile/games");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidIdentity.StatusCode);
    }

    [Fact]
    public async Task MissingOrPrivateGame_ReturnsSafeNotFound()
    {
        using var factory = new GamesReadFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(0));

        using var missing = await client.GetAsync($"/api/mobile/games/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var body = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.Equal("GAME_NOT_FOUND", body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("creatorUserId", out _));
    }

    [Fact]
    public async Task ReadDependencyFailure_ReturnsSafe503()
    {
        using var factory = new GamesReadFactory();
        factory.Available.Fail = true;
        factory.Details.Fail = true;
        factory.Attempts.Fail = true;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(0));

        foreach (var path in new[]
                 {
                     "/api/mobile/games",
                     $"/api/mobile/games/{factory.PublicGameId:D}",
                     "/api/mobile/games/me/attempts"
                 })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("GAMES_READ_UNAVAILABLE", body.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Swagger_ListsOnlyTheApprovedGamesReadRoutes()
    {
        using var factory = new GamesReadFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/mobile/games", out var list));
        Assert.True(list.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/mobile/games/{gameId}", out var detail));
        Assert.True(detail.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/mobile/games/me/attempts", out var attempts));
        Assert.True(attempts.TryGetProperty("get", out _));
        Assert.DoesNotContain(paths.EnumerateObject(), path =>
            path.Name.Contains("proof", StringComparison.OrdinalIgnoreCase) ||
            path.Name.Contains("reward", StringComparison.OrdinalIgnoreCase) ||
            path.Name.Contains("recovery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAttempt_MapsTrustedIdentityAndLifecycleResults_AndPublishesPostRoute()
    {
        using var factory = new GamesReadFactory();
        using var client = factory.CreateClient();
        var path = $"/api/mobile/games/{factory.PublicGameId:D}/attempts";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(0));
        using (var created = await client.PostAsync(path, content: null))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            Assert.False(body.RootElement.TryGetProperty("playerUserId", out _));
            Assert.Equal(factory.Account(0).UserId, factory.StartAttempts.LastPlayerUserId);
        }

        factory.StartAttempts.Resume = true;
        using (var resumed = await client.PostAsync(path, content: null))
            Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using (var anonymous = await client.PostAsync(path, content: null))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(2));
        using (var inactive = await client.PostAsync(path, content: null))
            Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-identity");
        using (var invalid = await client.PostAsync(path, content: null))
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(0));
        factory.StartAttempts.Missing = true;
        using (var missing = await client.PostAsync(path, content: null))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        factory.StartAttempts.Missing = false;
        factory.StartAttempts.Ineligible = true;
        using (var ineligible = await client.PostAsync(path, content: null))
            Assert.Equal(HttpStatusCode.Conflict, ineligible.StatusCode);
        factory.StartAttempts.Ineligible = false;
        factory.StartAttempts.Unavailable = true;
        using (var unavailable = await client.PostAsync(path, content: null))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);

        using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        using var doc = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("paths")
            .GetProperty("/api/mobile/games/{gameId}/attempts").TryGetProperty("post", out _));
    }

    [Fact]
    public async Task ConcurrentStartRetries_ReturnOneCreatedAttemptAndResumeTheSameAttempt()
    {
        using var factory = new GamesReadFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(0));
        var path = $"/api/mobile/games/{factory.PublicGameId:D}/attempts";

        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => client.PostAsync(path, content: null)));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Equal(11, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            var attemptIds = await Task.WhenAll(responses.Select(async response =>
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return body.RootElement.GetProperty("attemptId").GetGuid();
            }));
            Assert.Single(attemptIds.Distinct());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task DefaultRuntimeAttemptProvider_FailsClosedWith503()
    {
        using var factory = new GamesReadFactory { UseFakeAttemptStore = false };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Bearer(0));

        using var response = await client.PostAsync($"/api/mobile/games/{factory.PublicGameId:D}/attempts", content: null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task SqlEnabledConfiguration_WhenAttemptStoreUnavailable_Returns503()
    {
        using var factory = new GamesReadFactory
        {
            UseFakeAttemptStore = false,
            EnableSqlAttemptWriter = true
        };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Bearer(0));

        using var response = await client.PostAsync(
            $"/api/mobile/games/{factory.PublicGameId:D}/attempts", content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("GAME_ATTEMPTS_UNAVAILABLE", body.RootElement.GetProperty("code").GetString());
    }

    private sealed class GamesReadFactory : WebApplicationFactory<Program>
    {
        private const string Secret = "games-read-api-test-signing-secret-32-bytes-minimum";
        private readonly UserAccount[] _accounts = CreateAccounts();

        public readonly FakeAvailableGamesStore Available = new();
        public readonly FakeGameDetailsStore Details = new();
        public readonly FakeAttemptsStore Attempts = new();
        public readonly FakeStartAttemptStore StartAttempts = new();
        public bool UseFakeAttemptStore { get; set; } = true;
        public bool EnableSqlAttemptWriter { get; set; }
        public Guid PublicGameId { get; } = Guid.NewGuid();
        private readonly TestAccountIds _testAccountIds;

        public GamesReadFactory()
        {
            _testAccountIds = new TestAccountIds(_accounts.Select(x => x.UserId).ToArray());
            Available.GameId = PublicGameId;
            Details.GameId = PublicGameId;
            Attempts.OwnerUserId = _accounts[0].UserId;
        }

        public UserAccount Account(int index) => _accounts[index];

        public string Bearer(int index) => index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["environment"] = "Development",
                    ["Identity:EnableSharedAuthentication"] = "true",
                    ["Games:EnableSqlPersistence"] = EnableSqlAttemptWriter ? "true" : "false",
                    ["Games:EnableSqlAttemptWriter"] = EnableSqlAttemptWriter ? "true" : "false",
                    ["Games:EnableDevelopmentDraftHttpApi"] = "false",
                    ["TravelPlanning:EnableSqlPersistence"] = "false",
                    ["ConnectionStrings:WayhunterSharedDb"] = "Server=localhost;Database=GamesReadTests;Trusted_Connection=True;TrustServerCertificate=True",
                    ["Authentication:Jwt:Issuer"] = "wayhunter-games-test",
                    ["Authentication:Jwt:Audience"] = "wayhunter-mobile-test",
                    ["Authentication:Jwt:SigningSecret"] = Secret
                }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticatedSessionReader>();
                services.AddSingleton<IAuthenticatedSessionReader, UnusedSessionReader>();
                services.RemoveAll<ITrustedIdentityAccountReader>();
                services.AddSingleton<ITrustedIdentityAccountReader>(new TestAccountReader(_accounts));
                services.AddSingleton(_testAccountIds);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "GamesReadTest";
                    options.DefaultChallengeScheme = "GamesReadTest";
                    options.DefaultScheme = "GamesReadTest";
                }).AddScheme<AuthenticationSchemeOptions, GamesReadTestAuthHandler>("GamesReadTest", _ => { });
                services.RemoveAll<IAvailableGamesQueryStore>();
                services.AddSingleton<IAvailableGamesQueryStore>(Available);
                services.RemoveAll<IPublicGameDetailsQueryStore>();
                services.AddSingleton<IPublicGameDetailsQueryStore>(Details);
                services.RemoveAll<IMyGameAttemptsQueryStore>();
                services.AddSingleton<IMyGameAttemptsQueryStore>(Attempts);
                if (UseFakeAttemptStore)
                {
                    services.RemoveAll<IStartGameAttemptStore>();
                    services.AddSingleton<IStartGameAttemptStore>(StartAttempts);
                }
                else if (EnableSqlAttemptWriter)
                {
                    // Keep this HTTP test database-free; the actual SQL
                    // registration is asserted by M5GamesRuntimeDiStartupTests.
                    services.RemoveAll<IStartGameAttemptStore>();
                    services.AddSingleton<IStartGameAttemptStore>(
                        new UnavailableGameAttemptStore());
                }
            });
        }

        private static UserAccount[] CreateAccounts()
        {
            var created = DateTimeOffset.UtcNow.AddMinutes(-10);
            var active = Enumerable.Range(0, 2).Select(index =>
            {
                var account = UserAccount.RegisterPending(
                    $"Games reader {index}", $"games-{Guid.NewGuid():N}@example.test", "hash", created);
                account.ActivateAfterVerifiedOtp(created.AddMinutes(1));
                return account;
            }).ToArray();
            var inactive = UserAccount.RegisterPending(
                "Inactive reader", $"inactive-{Guid.NewGuid():N}@example.test", "hash", created);
            inactive.ActivateAfterVerifiedOtp(created.AddMinutes(1));
            inactive.Suspend();
            return [.. active, inactive];
        }
    }

    private sealed class TestAccountReader(UserAccount[] accounts) : ITrustedIdentityAccountReader
    {
        public Task<UserAccount?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(accounts.SingleOrDefault(x => x.UserId == userId));
        }
    }

    private sealed record TestAccountIds(string[] UserIds);

    private sealed class UnusedSessionReader : IAuthenticatedSessionReader
    {
        public Task<UserAccount?> FindAccountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(null);

        public Task<IdentitySession?> FindSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IdentitySession?>(null);
    }

    private sealed class GamesReadTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TestAccountIds _accounts;

        public GamesReadTestAuthHandler(
            Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder,
            TestAccountIds accounts)
            : base(options, logger, encoder) => _accounts = accounts;

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.NoResult());

            var token = header[7..];
            var userId = token == "invalid-identity"
                ? "not-a-guid"
                : int.TryParse(token, out var index) && index >= 0 && index < _accounts.UserIds.Length
                    ? _accounts.UserIds[index]
                    : null;
            if (userId is null)
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[]
            {
                new System.Security.Claims.Claim("sub", userId),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId)
            };
            var principal = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class FakeAvailableGamesStore : IAvailableGamesQueryStore
    {
        public Guid GameId { get; set; }
        public bool Fail { get; set; }
        public Task<IReadOnlyList<PublicGameSummaryDto>> GetAvailableGamesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            Fail ? Task.FromException<IReadOnlyList<PublicGameSummaryDto>>(new InvalidOperationException()) :
            Task.FromResult<IReadOnlyList<PublicGameSummaryDto>>([new(GameId, "Public game", null, 45, nowUtc.AddDays(-1), nowUtc.AddDays(29))]);
    }

    private sealed class FakeGameDetailsStore : IPublicGameDetailsQueryStore
    {
        public Guid GameId { get; set; }
        public bool Fail { get; set; }
        public Task<PublicGameDetailsDto?> GetPublicGameAsync(Guid gameId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            Fail ? Task.FromException<PublicGameDetailsDto?>(new InvalidOperationException()) :
            Task.FromResult(gameId == GameId ? new PublicGameDetailsDto(GameId, "Public game", null, 45, nowUtc.AddDays(-1), nowUtc.AddDays(29), 3) : null);
    }

    private sealed class FakeAttemptsStore : IMyGameAttemptsQueryStore
    {
        public bool Fail { get; set; }
        public string? OwnerUserId { get; set; }
        public string? LastRequestedUserId { get; private set; }
        public Task<IReadOnlyList<MyGameAttemptDto>> GetMyAttemptsAsync(string playerUserId, CancellationToken cancellationToken = default)
        {
            LastRequestedUserId = playerUserId;
            if (Fail)
                return Task.FromException<IReadOnlyList<MyGameAttemptDto>>(new InvalidOperationException());
            IReadOnlyList<MyGameAttemptDto> rows = playerUserId == OwnerUserId
                ? [new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), GameProgressStatus.InProgress, 1, 3)]
                : [];
            return Task.FromResult(rows);
        }
    }

    private sealed class FakeStartAttemptStore : IStartGameAttemptStore
    {
        public bool Resume { get; set; }
        public bool Missing { get; set; }
        public bool Ineligible { get; set; }
        public bool Unavailable { get; set; }
        public string? LastPlayerUserId { get; private set; }
        private readonly Guid _attemptId = Guid.NewGuid();
        private int _created;

        public Task<StartGameAttemptResultDto> StartOrResumeAsync(Guid gameId, string playerUserId,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            LastPlayerUserId = playerUserId;
            if (Missing) return Task.FromException<StartGameAttemptResultDto>(new KeyNotFoundException());
            if (Ineligible) return Task.FromException<StartGameAttemptResultDto>(new InvalidOperationException());
            if (Unavailable) return Task.FromException<StartGameAttemptResultDto>(new GameAttemptPersistenceUnavailableException("test"));
            var resumed = Resume || Interlocked.CompareExchange(ref _created, 1, 0) != 0;
            return Task.FromResult(new StartGameAttemptResultDto(_attemptId, gameId, playerUserId,
                nowUtc, nowUtc.AddHours(1), 1, resumed));
        }
    }
}
