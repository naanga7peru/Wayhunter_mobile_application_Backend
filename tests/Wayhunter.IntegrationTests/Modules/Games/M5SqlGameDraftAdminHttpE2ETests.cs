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
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Integrations.Email;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.Games;

public sealed class M5SqlGameDraftAdminHttpE2ETests
{
    private const string SigningSecret =
        "m5-sql-e2e-only-signing-secret-at-least-32-bytes";
    private const string AllowedDatabase =
        "Wayhunter_Test_M5_eee96080bb0540ebaa5cd326f366d2fb";

    [M5SqlIntegrationFact]
    public async Task AuthenticatedDraftCrudAndAdminReview_PersistThroughSql()
    {
        var connectionString = GetConnectionString();
        string? latestOtp = null;
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:WayhunterSharedDb", connectionString);
                builder.UseSetting("Identity:EnableSharedAuthentication", "true");
                builder.UseSetting("Games:EnableSqlPersistence", "true");
                builder.UseSetting("Authentication:Jwt:SigningSecret", SigningSecret);
                builder.UseSetting("Authentication:Jwt:Issuer", "wayhunter-m5-sql-e2e");
                builder.UseSetting("Authentication:Jwt:Audience", "wayhunter-m5-sql-e2e-mobile");
                builder.UseSetting("Identity:SessionLifetimeMinutes", "60");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:WayhunterSharedDb"] = connectionString,
                        ["Identity:EnableSharedAuthentication"] = "true",
                        ["Games:EnableSqlPersistence"] = "true",
                        ["Authentication:Jwt:SigningSecret"] = SigningSecret,
                        ["Authentication:Jwt:Issuer"] = "wayhunter-m5-sql-e2e",
                        ["Authentication:Jwt:Audience"] = "wayhunter-m5-sql-e2e-mobile",
                        ["Identity:SessionLifetimeMinutes"] = "60"
                    }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IIdentityOtpEmailSender>();
                    services.AddSingleton<IIdentityOtpEmailSender>(
                        new CapturingOtpSender(code => latestOtp = code));
                });
            });

        using var owner = factory.CreateClient();
        using (var anonymousRead = await owner.GetAsync(
                   $"/api/mobile/games/drafts/{Guid.NewGuid():D}"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousRead.StatusCode);

        var ownerToken = await RegisterAndLoginAsync(
            owner, "m5-owner-" + Guid.NewGuid().ToString("N") + "@example.test", () => latestOtp);
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);

        using var admin = factory.CreateClient();
        var adminEmail = "m5-admin-" + Guid.NewGuid().ToString("N") + "@example.test";
        var adminToken = await RegisterAndLoginAsync(
            admin, adminEmail, () => latestOtp);
        var adminId = await FindUserIdAsync(connectionString, adminEmail);
        await using (var db = CreateContext(connectionString))
        {
            db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(
                adminId, DateTimeOffset.UtcNow, null));
            await db.SaveChangesAsync();
        }
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var key = Guid.NewGuid().ToString("N");
        var draft = new CreateGameDraftDto("SQL E2E Draft", "Before review", 45,
            [new GameDraftStageDto(1, "Find the clock", null)]);
        using var create = await SendCreateAsync(owner, draft, key);
        Assert.True(create.StatusCode == HttpStatusCode.Created,
            $"Create returned {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        using var createJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var gameId = createJson.RootElement.GetProperty("gameId").GetGuid();
        var originalEtag = create.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrWhiteSpace(originalEtag));
        using (var crossOwnerRead = await admin.GetAsync($"/api/mobile/games/drafts/{gameId:D}"))
            Assert.Equal(HttpStatusCode.NotFound, crossOwnerRead.StatusCode);
        using (var nonAdminQueue = await owner.GetAsync("/api/admin/games/reviews/pending"))
            Assert.Equal(HttpStatusCode.Forbidden, nonAdminQueue.StatusCode);

        using var replay = await SendCreateAsync(owner, draft, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(gameId, replayJson.RootElement.GetProperty("gameId").GetGuid());

        using var read = await owner.GetAsync($"/api/mobile/games/drafts/{gameId:D}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(originalEtag, read.Headers.ETag?.Tag);

        var replacement = new CreateGameDraftDto("SQL E2E Updated", "After update", 60,
            [new GameDraftStageDto(1, "Read the clock", null)]);
        using var update = await SendUpdateAsync(owner, gameId, replacement, originalEtag!);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updatedEtag = update.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrWhiteSpace(updatedEtag));
        Assert.NotEqual(originalEtag, updatedEtag);

        using var staleUpdate = await SendUpdateAsync(owner, gameId, draft, originalEtag!);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        using var submit = await owner.PostAsync($"/api/mobile/games/drafts/{gameId:D}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        using var queue = await admin.GetAsync("/api/admin/games/reviews/pending");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        using var queueJson = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());
        Assert.Contains(queueJson.RootElement.EnumerateArray(),
            item => item.GetProperty("gameId").GetGuid() == gameId);

        using var approve = await admin.PostAsync($"/api/admin/games/reviews/{gameId:D}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        using (var db = CreateContext(connectionString))
        {
            var approved = await db.Games.SingleAsync(x => x.Id == gameId);
            Assert.Equal(GameStatus.Published, approved.Status);
            Assert.Equal(TimeSpan.FromDays(30),
                approved.ExpiresAtUtc!.Value - approved.ApprovedAtUtc!.Value);
            Assert.Single(await db.GameCreationApprovals.Where(x => x.GameId == gameId).ToListAsync());
        }

        using var delete = await owner.DeleteAsync($"/api/mobile/games/drafts/{gameId:D}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var rejectedDraft = new CreateGameDraftDto("SQL E2E Rejected", null, 30,
            [new GameDraftStageDto(1, "A clue to reject", null)]);
        using var rejectedCreate = await SendCreateAsync(
            owner, rejectedDraft, Guid.NewGuid().ToString("N"));
        Assert.True(rejectedCreate.StatusCode == HttpStatusCode.Created,
            $"Rejected draft create returned {(int)rejectedCreate.StatusCode}: {await rejectedCreate.Content.ReadAsStringAsync()}");
        using var rejectedJson = JsonDocument.Parse(
            await rejectedCreate.Content.ReadAsStringAsync());
        var rejectedGameId = rejectedJson.RootElement.GetProperty("gameId").GetGuid();
        using var rejectedSubmit = await owner.PostAsync(
            $"/api/mobile/games/drafts/{rejectedGameId:D}/submit", null);
        Assert.Equal(HttpStatusCode.OK, rejectedSubmit.StatusCode);
        using var reject = await admin.PostAsJsonAsync(
            $"/api/admin/games/reviews/{rejectedGameId:D}/reject",
            new { reason = "Insufficient clue detail" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        await using (var db = CreateContext(connectionString))
        {
            var rejected = await db.Games.SingleAsync(x => x.Id == rejectedGameId);
            Assert.Equal(GameStatus.Rejected, rejected.Status);
            Assert.Single(await db.GameModerationDecisions
                .Where(x => x.GameId == rejectedGameId).ToListAsync());
        }
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("M5 isolated SQL test database is not configured.");
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, AllowedDatabase, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the approved isolated M5 LocalDB test database is allowed.");
        return value;
    }

    private static async Task<string> RegisterAndLoginAsync(
        HttpClient client, string email, Func<string?> getOtp)
    {
        const string password = "M5-E2e-Password-2026!";
        using var register = await client.PostAsJsonAsync("/api/mobile/identity/register",
            new { displayName = "M5 SQL E2E", email, password, confirmPassword = password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(getOtp()));
        using var verify = await client.PostAsJsonAsync("/api/mobile/identity/verify-otp",
            new { email, code = getOtp() });
        Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
        Assert.True(login.StatusCode == HttpStatusCode.OK,
            $"Login returned {(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendCreateAsync(
        HttpClient client, CreateGameDraftDto draft, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/mobile/games/drafts");
        request.Headers.Add("Idempotency-Key", key);
        request.Content = JsonContent.Create(draft);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendUpdateAsync(
        HttpClient client, Guid gameId, CreateGameDraftDto draft, string etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/mobile/games/drafts/{gameId:D}");
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        request.Content = JsonContent.Create(draft);
        return await client.SendAsync(request);
    }

    private static async Task<string> FindUserIdAsync(string connectionString, string email)
    {
        await using var db = CreateContext(connectionString);
        return await db.Users.Where(x => x.NormalizedEmail == email.ToUpperInvariant())
            .Select(x => x.UserId).SingleAsync();
    }

    private static WayhunterSharedDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseSqlServer(connectionString).Options);

    private sealed class CapturingOtpSender(Action<string> capture) : IIdentityOtpEmailSender
    {
        public Task<bool> SendAsync(string recipientEmail, OtpEmailContent content,
            CancellationToken cancellationToken = default)
        {
            var text = string.Join(" ", content.GetType().GetProperties()
                .Select(property => property.GetValue(content)?.ToString()));
            var code = System.Text.RegularExpressions.Regex.Match(text, @"(?<!\d)\d{6}(?!\d)").Value;
            if (code.Length != 6) return Task.FromResult(false);
            capture(code);
            return Task.FromResult(true);
        }
    }
}
