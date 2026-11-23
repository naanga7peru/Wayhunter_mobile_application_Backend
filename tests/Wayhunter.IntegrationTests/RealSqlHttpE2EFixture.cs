using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Infrastructure.Integrations.Email;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.IntegrationTests;

/// <summary>
/// Opt-in authenticated SQL HTTP acceptance tests. Run only against an isolated
/// LocalDB catalog created and owned by this fixture; never uses appsettings DBs.
/// </summary>
public sealed class RealSqlHttpE2EFixture : IAsyncLifetime
{
    private readonly string _databaseName = "WayhunterE2E_" + Guid.NewGuid().ToString("N");
    private WebApplicationFactory<Program>? _factory;
    private string? _connectionString;
    private bool _created;

    public HttpClient Client { get; private set; } = null!;
    public string? OtpCode { get; private set; }
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        var master = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true
        }.ConnectionString;
        await using (var connection = new SqlConnection(master))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
            _created = true;
        }

        _connectionString = new SqlConnectionStringBuilder(master)
        {
            InitialCatalog = _databaseName
        }.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:WayhunterSharedDb", ConnectionString);
            builder.UseSetting("Identity:EnableSharedAuthentication", "true");
            builder.UseSetting("TravelPlanning:EnableSqlPersistence", "true");
            builder.UseSetting("TripSafety:EnableSqlPersistence", "true");
            builder.UseSetting("Games:EnableSqlPersistence", "true");
            builder.UseSetting("Games:EnableSqlAttemptWriter", "true");
            builder.UseSetting("Advertisements:EnableSqlPersistence", "true");
            builder.UseSetting("CoinsWallet:EnableSqlRewardWriter", "false");
            builder.UseSetting("CoinsWallet:EnableSqlRewardOutboxStore", "false");
            builder.UseSetting("CoinsWallet:EnableRewardOutboxWorker", "false");
            builder.UseSetting("CoinsWallet:EnableSqlVoucherRuntime", "false");
            builder.UseSetting("Authentication:Jwt:SigningSecret", "e2e-only-signing-secret-with-at-least-32-bytes");
            builder.UseSetting("Authentication:Jwt:Issuer", "wayhunter-sql-e2e");
            builder.UseSetting("Authentication:Jwt:Audience", "wayhunter-sql-e2e-mobile");
            builder.UseSetting("Identity:SessionLifetimeMinutes", "60");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WayhunterSharedDb"] = ConnectionString,
                    ["Identity:EnableSharedAuthentication"] = "true",
                    ["TravelPlanning:EnableSqlPersistence"] = "true",
                    ["TripSafety:EnableSqlPersistence"] = "true",
                    ["Games:EnableSqlPersistence"] = "true",
                    ["Games:EnableSqlAttemptWriter"] = "true",
                    ["Advertisements:EnableSqlPersistence"] = "true",
                    ["CoinsWallet:EnableSqlRewardWriter"] = "false",
                    ["CoinsWallet:EnableSqlRewardOutboxStore"] = "false",
                    ["CoinsWallet:EnableRewardOutboxWorker"] = "false",
                    ["CoinsWallet:EnableSqlVoucherRuntime"] = "false",
                    ["Authentication:Jwt:SigningSecret"] = "e2e-only-signing-secret-with-at-least-32-bytes",
                    ["Authentication:Jwt:Issuer"] = "wayhunter-sql-e2e",
                    ["Authentication:Jwt:Audience"] = "wayhunter-sql-e2e-mobile",
                    ["Identity:SessionLifetimeMinutes"] = "60"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentityOtpEmailSender>();
                services.AddSingleton<IIdentityOtpEmailSender>(new CapturingOtpSender(code => OtpCode = code));
            });
        });

        // Apply the three checked-in migrations to this fixture-owned catalog.
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(ConnectionString).Options;
        await using (var db = new WayhunterSharedDbContext(options))
            await db.Database.MigrateAsync();
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_created)
        {
            var master = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
            _created = false;
        }
    }

    public async Task<string> RegisterAndLoginAsync()
    {
        const string email = "sql-e2e@wayhunter.invalid";
        const string password = "E2e-Password-2026!";
        using var register = await Client.PostAsJsonAsync("/api/mobile/identity/register",
            new { displayName = "SQL E2E", email, password, confirmPassword = password });
        Assert.True(register.StatusCode == HttpStatusCode.Created,
            $"Registration returned {(int)register.StatusCode}: {await register.Content.ReadAsStringAsync()}");
        Assert.False(string.IsNullOrWhiteSpace(OtpCode));
        using var verify = await Client.PostAsJsonAsync("/api/mobile/identity/verify-otp", new { email, code = OtpCode });
        Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());
        using var login = await Client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
        Assert.True(login.StatusCode == HttpStatusCode.OK,
            $"Login returned {(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
        using var payload = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = payload.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token!;
    }

    [SqlE2EFact]
    public async Task M1_RegistrationOtpLoginAndProfileUseSqlAndRealBearer()
    {
        await RegisterAndLoginAsync();
        using var me = await Client.GetAsync("/api/mobile/identity/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        await using var db = CreateVerificationContext();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "sql-e2e@wayhunter.invalid"));
        Assert.Equal(1, await db.IdentitySessions.CountAsync());
    }

    [SqlE2EFact]
    public async Task M2_AuthenticatedTripCrudPersistsInSql()
    {
        await RegisterAndLoginAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var create = await Client.PostAsJsonAsync("/api/mobile/trip-plans", new CreateTripPlanDto(
            "HTTP SQL trip", today.AddDays(1), today.AddDays(3), 25000m, "Car", 2));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var trip = await create.Content.ReadFromJsonAsync<TripPlanResponseDto>();
        Assert.NotNull(trip);
        using var list = await Client.GetAsync("/api/mobile/trip-plans");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var detail = await Client.GetAsync($"/api/mobile/trip-plans/{trip.TripPlanId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var updateRequest = new UpdateTripPlanDto("Updated HTTP SQL trip", today.AddDays(1), today.AddDays(4), 30000m, "Car", 2)
        { ExpectedRowVersion = trip.RowVersion };
        var updated = await Client.PutAsJsonAsync($"/api/mobile/trip-plans/{trip.TripPlanId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await using var db = CreateVerificationContext();
        Assert.Equal("Updated HTTP SQL trip", await db.Trips.Where(x => x.Id == trip.TripPlanId).Select(x => x.TripName).SingleAsync());
    }

    [SqlE2EFact]
    public async Task M5M6M7_AuthenticatedReadsAndGameAttemptUseSql()
    {
        using var anonymous = await Client.GetAsync("/api/mobile/games");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var anonymousStart = await Client.PostAsync($"/api/mobile/games/{Guid.NewGuid()}/attempts", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStart.StatusCode);
        await RegisterAndLoginAsync();

        using var games = await Client.GetAsync("/api/mobile/games");
        Assert.Equal(HttpStatusCode.OK, games.StatusCode);
        var creator = UserAccount.RegisterPending("SQL E2E creator",
            $"creator-{Guid.NewGuid():N}@wayhunter.invalid", "test-only-hash", DateTimeOffset.UtcNow.AddMinutes(-2));
        creator.ActivateAfterVerifiedOtp(DateTimeOffset.UtcNow.AddMinutes(-2));
        var game = Game.CreateDraft(Guid.NewGuid(), creator.UserId, "HTTP SQL E2E game", "Fixture game", 60);
        var step = new GameStep(Guid.NewGuid(), game.Id, 1);
        var hint = new GameHint(Guid.NewGuid(), step.Id, "Find the first checkpoint", null);
        game.ReplaceDraftStages(creator.UserId, new[] { step }, new[] { hint });
        game.SubmitForAdminReview(creator.UserId, DateTimeOffset.UtcNow.AddMinutes(-1));
        game.ApproveCreation(DateTimeOffset.UtcNow);
        await using (var db = CreateVerificationContext())
        {
            db.Users.Add(creator);
            await db.SaveChangesAsync();
            await new SqlGameRepository(db).AddAsync(game);
        }

        using var detail = await Client.GetAsync($"/api/mobile/games/{game.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var start = await Client.PostAsync($"/api/mobile/games/{game.Id}/attempts", null);
        Assert.True(start.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await start.Content.ReadAsStringAsync());
        using var firstAttempt = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
        var attemptId = firstAttempt.RootElement.GetProperty("attemptId").GetGuid();
        using var retry = await Client.PostAsync($"/api/mobile/games/{game.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var resumedAttempt = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal(attemptId, resumedAttempt.RootElement.GetProperty("attemptId").GetGuid());
        await using (var db = CreateVerificationContext())
        {
            Assert.Equal(1, await db.GameAttempts.CountAsync(x => x.GameId == game.Id));
            Assert.Equal(1, await db.GameAttemptProgress.CountAsync(x => x.GameId == game.Id));
        }

        await using (var db = CreateVerificationContext())
        {
            var userId = await db.Users.Where(x => x.NormalizedEmail == "sql-e2e@wayhunter.invalid")
                .Select(x => x.UserId).SingleAsync();
            db.CoinWallets.Add(new CoinWalletRow
            {
                Id = Guid.NewGuid(), UserId = userId, PostedCoins = 175,
                ReservedCoins = 25, LifetimeEarnedCoins = 200
            });
            await db.SaveChangesAsync();
        }
        using var wallet = await Client.GetAsync("/api/mobile/coins-wallet/me");
        Assert.Equal(HttpStatusCode.OK, wallet.StatusCode);
        using var balance = JsonDocument.Parse(await wallet.Content.ReadAsStringAsync());
        Assert.Equal(175, balance.RootElement.GetProperty("postedCoins").GetInt64());
        Assert.Equal(25, balance.RootElement.GetProperty("reservedCoins").GetInt64());
        using var transactions = await Client.GetAsync("/api/mobile/coins-wallet/me/transactions");
        Assert.Equal(HttpStatusCode.OK, transactions.StatusCode);
        using var advertisements = await Client.GetAsync("/api/mobile/advertisements/active");
        Assert.Equal(HttpStatusCode.OK, advertisements.StatusCode);
    }

    private WayhunterSharedDbContext CreateVerificationContext() => new(
        new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(ConnectionString).Options);

    private sealed class CapturingOtpSender(Action<string> capture) : IIdentityOtpEmailSender
    {
        public Task<bool> SendAsync(string recipientEmail, OtpEmailContent content, CancellationToken cancellationToken = default)
        {
            var text = string.Join(" ", content.GetType().GetProperties().Select(p => p.GetValue(content)?.ToString()));
            var code = System.Text.RegularExpressions.Regex.Match(text, @"(?<!\d)\d{6}(?!\d)").Value;
            if (code.Length != 6) return Task.FromResult(false);
            capture(code);
            return Task.FromResult(true);
        }
    }
}

public sealed class SqlE2EFactAttribute : FactAttribute
{
    public SqlE2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_SQL_E2E") != "1")
            Skip = "Set WAYHUNTER_SQL_E2E=1 to run LocalDB HTTP-to-SQL acceptance tests.";
    }
}
