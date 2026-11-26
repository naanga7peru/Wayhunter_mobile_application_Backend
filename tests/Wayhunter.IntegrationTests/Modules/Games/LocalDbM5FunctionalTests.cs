using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed partial class LocalDbAuthenticationPhase3Tests
{
    [LocalDbM5Fact]
    public Task VerifyM5Operations()
    {
        var catalog = Environment.GetEnvironmentVariable("WAYHUNTER_M5_FUNCTIONAL_CATALOG");
        Assert.True(catalog is not null && Regex.IsMatch(catalog, @"\AWayhunter_M5_Functional_[a-f0-9]{32}\z"));
        var sql = Environment.GetEnvironmentVariable("Games__EnableSqlPersistence") == "true";
        var fault = new M5CommitFault();
        return RunIsolatedAsync(catalog!, migrateFreshCatalog: true,
            (client, db, capture, clock, _) =>
            {
                clock.Offset = TimeSpan.Zero;
                return VerifyGamesAsync(client, db, capture, clock, sql, fault);
            }, services => services.ConfigureDbContext<WayhunterSharedDbContext>(options => options.AddInterceptors(fault)));
    }

    private async Task VerifyGamesAsync(HttpClient client, WayhunterSharedDbContext db,
        Capture capture, TestClock clock, bool sql, M5CommitFault fault)
    {
        const string games = "/api/mobile/games";
        const string item = games + "/{gameId}";
        const string drafts = games + "/drafts";
        const string draft = drafts + "/{gameId}";
        const string submit = draft + "/submit";
        const string attempts = item + "/attempts";
        const string reviews = "/api/admin/games/reviews";
        const string approve = reviews + "/{gameId}/approve";
        const string reject = reviews + "/{gameId}/reject";
        var missing = Guid.NewGuid().ToString("D");
        (string Method, string Route, bool Gated)[] operations =
        [
            ("GET", games, false), ("GET", item, false), ("GET", games + "/me", false),
            ("GET", games + "/me/attempts", false), ("POST", attempts, false),
            ("POST", drafts, true), ("GET", draft, true), ("PUT", draft, true), ("DELETE", draft, true),
            ("POST", submit, true), ("GET", reviews + "/pending", true), ("POST", approve, true), ("POST", reject, true)
        ];
        using (var spec = await client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json"))
        {
            var paths = spec!.RootElement.GetProperty("paths");
            foreach (var (method, route, gated) in operations)
                Assert.Equal(sql || !gated, paths.TryGetProperty(route, out var path) &&
                    path.TryGetProperty(method.ToLowerInvariant(), out _));
        }

        foreach (var token in new string?[] { null, "invalid.jwt.token" })
        {
            Use(token);
            foreach (var (method, route, gated) in operations)
                await Check(method, route, !sql && gated ? 404 : 401, alternate: !sql && gated ? 405 : null);
        }
        var creator = await Register("Game creator");
        Use(creator.Token);
        if (!sql)
        {
            foreach (var (method, route, gated) in operations)
                await Check(method, route, gated ? 404 : 503, alternate: gated ? 405 : null);
            Assert.Empty(await db.Games.ToListAsync());
            Assert.Empty(await db.GameAttempts.ToListAsync());
            return;
        }

        var player = await Register("Player");
        var outsider = await Register("Other player");
        var admin = await Register("Game administrator");
        db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(admin.Id, DateTimeOffset.UtcNow, null));
        await db.SaveChangesAsync();
        Use(creator.Token);
        var request = new CreateGameDraftDto("Fixture game", "Text-only", 45, [new(1, "Fixture clue", null)]);
        await Check("POST", drafts, 400, body: request);
        await Check("POST", drafts, 400, body: request with { DurationMinutes = 0 }, key: "invalid");
        fault.Armed = true;
        await Check("POST", drafts, 409, body: request, key: "committed-on-retry");
        Assert.False(fault.Armed);
        Assert.Empty(await db.Games.ToListAsync());
        Assert.Empty(await db.GameDraftCreateOperations.ToListAsync());
        var created = await Task.WhenAll(
            Check("POST", drafts, 201, body: request, key: "committed-on-retry"),
            Check("POST", drafts, 201, body: request, key: "committed-on-retry"));
        var id = created[0].Body.GetProperty("gameId").GetGuid();
        Assert.Equal(id, created[1].Body.GetProperty("gameId").GetGuid());
        Assert.Single(await db.GameDraftCreateOperations.ToListAsync());
        var revision = Assert.IsType<string>(created[0].Revision);
        var url = $"{drafts}/{id}";
        await Check("POST", drafts, 409, body: request with { Title = "Different payload" }, key: "committed-on-retry");

        Use(player.Token);
        Assert.Empty((await Check("GET", games, 200)).Body.EnumerateArray());
        await Check("GET", item, 404, $"{games}/{id}");
        await Check("POST", attempts, 404, $"{games}/{id}/attempts");
        await Check("GET", draft, 404, url);
        await Check("PUT", draft, 404, url, request, revision: revision);
        await Check("DELETE", draft, 404, url);
        await Check("POST", submit, 404, url + "/submit");
        await Check("POST", submit, 404);
        await Check("GET", reviews + "/pending", 403);
        await Check("POST", approve, 403, $"{reviews}/{id}/approve");
        await Check("POST", reject, 403, $"{reviews}/{id}/reject", new { reason = "Fixture" });

        Use(creator.Token);
        var before = await Check("GET", draft, 200, url);
        Assert.Equal(revision, before.Revision);
        Assert.DoesNotContain(before.Body.EnumerateObject(), x => x.Name is "creatorUserId" or "reward" or "proof");
        var update = await Check("PUT", draft, 200, url, request with { Title = "Updated fixture" }, revision: revision);
        Assert.NotEqual(revision, update.Revision);
        await Check("PUT", draft, 409, url, request, revision: revision);
        var discarded = await Check("POST", drafts, 201, body: request, key: "delete");
        var discardedId = discarded.Body.GetProperty("gameId").GetGuid();
        await Check("DELETE", draft, 204, $"{drafts}/{discardedId}");
        Assert.False(await db.Games.AnyAsync(x => x.Id == discardedId));
        await Check("POST", submit, 200, url + "/submit");
        await Check("DELETE", draft, 409, url);
        Assert.True(await db.Games.AnyAsync(x => x.Id == id && x.Status == GameStatus.PendingApproval && x.Title == "Updated fixture"));

        var second = await Check("POST", drafts, 201, body: request, key: "reject");
        var rejectedId = second.Body.GetProperty("gameId").GetGuid();
        await Check("POST", submit, 200, $"{drafts}/{rejectedId}/submit");
        Use(admin.Token);
        var queue = await Check("GET", reviews + "/pending", 200);
        Assert.Equal(2, queue.Body.GetArrayLength());
        await Check("POST", approve, 404, $"{reviews}/{missing}/approve");
        await Check("POST", reject, 400, $"{reviews}/{rejectedId}/reject", new { reason = "" });
        await Check("POST", reject, 200, $"{reviews}/{rejectedId}/reject", new { reason = "Fixture moderation" });
        await Check("POST", reject, 409, $"{reviews}/{rejectedId}/reject", new { reason = "Repeat" });
        var approvals = await Task.WhenAll(
            Send("POST", $"{reviews}/{id}/approve", new { amount = 9000, rewardCoins = 9000 }),
            Send("POST", $"{reviews}/{id}/approve"));
        var statuses = approvals.Select(x => x.Code).Order().ToArray();
        output.WriteLine($"Concurrent approval expected 200/409, actual {string.Join("/", statuses)}.");
        Assert.Equal(new[] { 200, 409 }, statuses);
        var approval = await db.GameCreationApprovals.AsNoTracking().SingleAsync();
        Assert.Equal(id, approval.GameId);
        Assert.Equal(admin.Id, approval.AdminUserId);
        var game = await db.Games.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(GameStatus.Published, game.Status);
        Assert.Equal(TimeSpan.FromDays(30), game.ExpiresAtUtc - game.ApprovedAtUtc);
        Assert.True(await db.GameModerationDecisions.AnyAsync(x => x.GameId == rejectedId));
        Assert.Empty((await Check("GET", reviews + "/pending", 200)).Body.EnumerateArray());

        Use(creator.Token);
        var own = await Check("GET", games + "/me", 200);
        Assert.Contains(own.Body.EnumerateArray(), x => x.GetProperty("gameId").GetGuid() == id);
        await Check("POST", attempts, 409, $"{games}/{id}/attempts");
        Use(player.Token);
        Assert.Empty((await Check("GET", games + "/me", 200)).Body.EnumerateArray());
        var published = await Check("GET", games, 200);
        Assert.Equal(id, Assert.Single(published.Body.EnumerateArray()).GetProperty("gameId").GetGuid());
        var details = await Check("GET", item, 200, $"{games}/{id}");
        Assert.DoesNotContain(details.Body.EnumerateObject(), x => x.Name is "creatorUserId" or "answer" or "privateMediaReference");
        await Check("GET", item, 404, $"{games}/{rejectedId}");
        await Check("POST", attempts, 404, $"{games}/{rejectedId}/attempts");
        await Check("POST", attempts, 404, $"{games}/{missing}/attempts");
        var starts = await Task.WhenAll(Send("POST", $"{games}/{id}/attempts"),
            Send("POST", $"{games}/{id}/attempts"));
        var startCodes = starts.Select(x => x.Code).Order().ToArray();
        output.WriteLine($"Concurrent attempt expected 200/201, actual {string.Join("/", startCodes)}.");
        Assert.Equal(new[] { 200, 201 }, startCodes);
        var attemptId = starts[0].Body.GetProperty("attemptId").GetGuid();
        Assert.Equal(attemptId, starts[1].Body.GetProperty("attemptId").GetGuid());
        Assert.Single(await db.GameAttempts.Where(x => x.GameId == id).ToListAsync());
        Assert.Single(await db.GameAttemptProgress.Where(x => x.GameId == id).ToListAsync());
        var history = await Check("GET", games + "/me/attempts", 200);
        Assert.Equal(attemptId, Assert.Single(history.Body.EnumerateArray()).GetProperty("attemptId").GetGuid());
        Use(outsider.Token);
        Assert.Empty((await Check("GET", games + "/me/attempts", 200)).Body.EnumerateArray());
        Assert.Empty(await db.GameWinners.ToListAsync());
        Assert.Empty(await db.CoinRewardOutbox.ToListAsync());
        Assert.Empty(await db.CoinLedgerEntries.ToListAsync());
        var expired = Game.CreateDraft(Guid.NewGuid(), creator.Id, "Historical fixture", null, 45);
        var step = new GameStep(Guid.NewGuid(), expired.Id, 1);
        expired.ReplaceDraftStages(creator.Id, [step], [new GameHint(Guid.NewGuid(), step.Id, "Historical text clue", null)]);
        expired.SubmitForAdminReview(creator.Id, clock.GetUtcNow().AddDays(-32));
        expired.ApproveCreation(clock.GetUtcNow().AddDays(-31));
        var gameStore = new SqlGameRepository(db);
        await gameStore.AddAsync(expired);
        await Check("GET", item, 404, $"{games}/{expired.Id}");
        await Check("POST", attempts, 409, $"{games}/{expired.Id}/attempts");
        Assert.Equal(1, await gameStore.ArchiveExpiredGamesAsync(clock.GetUtcNow(), 10));
        Assert.True(await db.Games.AnyAsync(x => x.Id == expired.Id && x.Status == GameStatus.Expired));
        Use(creator.Token);
        var archived = await Check("GET", games + "/me", 200);
        Assert.Contains(archived.Body.EnumerateArray(), x => x.GetProperty("gameId").GetGuid() == expired.Id);

        void Use(string? token) => client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

        async Task<(int Code, JsonElement Body, string? Revision)> Send(string method, string path,
            object? body = null, string? key = null, string? revision = null)
        {
            using var req = new HttpRequestMessage(new HttpMethod(method), path);
            if (body is not null) req.Content = JsonContent.Create(body);
            if (key is not null) req.Headers.Add("Idempotency-Key", key);
            if (revision is not null) req.Headers.IfMatch.ParseAdd(revision);
            using var res = await client.SendAsync(req);
            var json = res.IsSuccessStatusCode && res.Content.Headers.ContentType?.MediaType == "application/json"
                ? await res.Content.ReadFromJsonAsync<JsonElement>() : default;
            return ((int)res.StatusCode, json, res.Headers.ETag?.Tag);
        }

        async Task<(JsonElement Body, string? Revision)> Check(string method, string route, int expected,
            string? concrete = null, object? body = null, string? key = null, string? revision = null,
            int? alternate = null)
        {
            var result = await Send(method, concrete ?? route.Replace("{gameId}", missing), body, key, revision);
            var expectedCodes = alternate is int additional ? $"{expected}/{additional}" : expected.ToString();
            output.WriteLine($"{method} {route}: expected {expectedCodes}, actual {result.Code}.");
            Assert.True(result.Code == expected || result.Code == alternate,
                $"Expected {expectedCodes}, got {result.Code} for {method} {route}.");
            return (result.Body, result.Revision);
        }

        async Task<(string Id, string Token)> Register(string name)
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            var password = Guid.NewGuid().ToString("N") + "A!";
            using var registration = await client.PostAsJsonAsync("/api/mobile/identity/register",
                new { displayName = name, email, password, confirmPassword = password });
            Assert.Equal(201, (int)registration.StatusCode);
            using var verify = await client.PostAsJsonAsync("/api/mobile/identity/verify-otp", new { email, code = capture.Codes.Last() });
            Assert.Equal(204, (int)verify.StatusCode);
            using var signIn = await client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
            Assert.Equal(200, (int)signIn.StatusCode);
            using var data = (await signIn.Content.ReadFromJsonAsync<JsonDocument>())!;
            return (data.RootElement.GetProperty("userId").GetString()!, data.RootElement.GetProperty("accessToken").GetString()!);
        }
    }

    private sealed class M5CommitFault : DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Armed = false;
                throw new InvalidOperationException("Synthetic pre-commit failure.");
            }
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }
}

public sealed class LocalDbM5FactAttribute : FactAttribute
{
    public LocalDbM5FactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_LOCAL_M5_FUNCTIONAL") != "1")
            Skip = "Requires explicit M5 opt-in and a fresh dedicated LocalDB catalog.";
    }
}
