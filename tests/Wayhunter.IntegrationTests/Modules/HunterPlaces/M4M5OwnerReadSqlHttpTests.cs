using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.HunterPlaces;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

[AttributeUsage(AttributeTargets.Method)]
public sealed class M4M5SqlHttpFactAttribute : FactAttribute
{
    public M4M5SqlHttpFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M4M5_TEST_SQL_CONNECTION")))
            Skip = "A uniquely named disposable M4/M5 LocalDB catalog is not configured.";
    }
}

public sealed class M4M5OwnerReadSqlHttpTests
{
    private const string ConnectionVariable = "WAYHUNTER_M4M5_TEST_SQL_CONNECTION";

    [M4M5SqlHttpFact]
    public async Task AuthenticatedOwnerReadsOnlyTheirSubmissionsApprovedPlacesAndGamesFromSql()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var sql = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
        Assert.Matches(@"^Wayhunter_Test_[A-Za-z0-9_-]+_[0-9a-fA-F]{32}$", sql.InitialCatalog);
        Assert.Equal(@"(localdb)\MSSQLLocalDB", sql.DataSource, ignoreCase: true);

        var now = DateTimeOffset.UtcNow;
        var owner = UserAccount.RegisterPending("SQL API owner", $"owner-{Guid.NewGuid():N}@example.test", "hash", now.AddDays(-1));
        owner.ActivateAfterVerifiedOtp(now.AddHours(-1));
        var foreign = UserAccount.RegisterPending("SQL API foreign", $"foreign-{Guid.NewGuid():N}@example.test", "hash", now.AddDays(-1));
        foreign.ActivateAfterVerifiedOtp(now.AddHours(-1));
        var tokenIdentity = new TestIdentity(owner);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration["Games:EnableSqlPersistence"] = "true";
        builder.Configuration["Identity:AdministratorRole"] = "Admin";
        builder.Configuration["ConnectionStrings:WayhunterSharedDb"] = connection;
        builder.Services.AddControllers().AddApplicationPart(typeof(HunterSubmissionsController).Assembly);
        builder.Services.AddDbContext<WayhunterSharedDbContext>(options => options.UseSqlServer(connection));
        builder.Services.AddSingleton<ITrustedIdentityAccountReader>(new TestAccountReader(owner));
        builder.Services.AddScoped<IHunterSubmissionStatusReader, SqlHunterSubmissionStatusReader>();
        builder.Services.AddScoped<HunterSubmissionStatusService>();
        builder.Services.AddScoped<IHunterDraftStore, SqlHunterDraftStore>();
        builder.Services.AddScoped<HunterDraftService>();
        builder.Services.AddScoped<IHunterReviewStore, SqlHunterReviewStore>();
        builder.Services.AddScoped<IHunterReviewService, HunterReviewService>();
        builder.Services.AddScoped<IHunterCaptureSubmissionAccessReader, SqlHunterCaptureSubmissionAccessReader>();
        builder.Services.AddScoped<IHunterCaptureSessionStore, SqlHunterCaptureSessionStore>();
        builder.Services.AddScoped<HunterCaptureSubmissionOwnershipService>();
        builder.Services.AddScoped<HunterCaptureSessionIdentityResolutionService>();
        builder.Services.AddScoped<HunterCaptureSessionService>();
        builder.Services.AddScoped<Wayhunter.Application.Modules.HunterPlaces.Commands.HunterStartCaptureSessionHandler>();
        builder.Services.AddScoped<Wayhunter.Application.Modules.HunterPlaces.Commands.HunterStartAuthorizedCaptureSessionHandler>();
        builder.Services.AddScoped<IHunterCaptureSessionIdentityResolver, Wayhunter.Api.Services.HunterPlaces.TrustedHunterCaptureSessionIdentityResolver>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddGamesApplicationServices();
        builder.Services.AddGamesReadApiServices(builder.Configuration);
        builder.Services.AddScoped<TrustedIdentityResolutionService>();
        builder.Services.AddSingleton(tokenIdentity);
        builder.Services.AddAuthentication("SqlHttpTest")
            .AddScheme<AuthenticationSchemeOptions, SqlHttpTestAuthHandler>("SqlHttpTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddCoinsWalletAdministratorPolicy(builder.Configuration);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "admin");

        var submissionId = Guid.NewGuid();
        var foreignSubmissionId = Guid.NewGuid();
        var pendingSubmissionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var foreignGameId = Guid.NewGuid();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WayhunterSharedDbContext>();
            db.Users.AddRange(owner, foreign);
            db.HunterSubmissions.AddRange(
                new HunterSubmission { Id = submissionId, UserId = owner.UserId, UserNameSnapshot = "private", PlaceName = "Owner place", District = "Jaffna", Latitude = 9.6, Longitude = 80.0, Status = HunterSubmissionStatus.Approved, CreatedAtUtc = now, SubmittedAtUtc = now.AddMinutes(-5) },
                new HunterSubmission { Id = foreignSubmissionId, UserId = foreign.UserId, UserNameSnapshot = "private", PlaceName = "Foreign place", District = "Colombo", Latitude = 6.9, Longitude = 79.8, Status = HunterSubmissionStatus.Approved, CreatedAtUtc = now, SubmittedAtUtc = now.AddMinutes(-5) },
                new HunterSubmission { Id = pendingSubmissionId, UserId = foreign.UserId, UserNameSnapshot = "private", PlaceName = "Pending review", District = "Kandy", Latitude = 7.3, Longitude = 80.6, Status = HunterSubmissionStatus.PendingReview, CreatedAtUtc = now, SubmittedAtUtc = now.AddMinutes(-2) });
            db.HunterReviewDecisions.AddRange(
                new HunterReviewDecision { HunterSubmissionId = submissionId, ReviewerUserId = foreign.UserId, ResultingStatus = HunterSubmissionStatus.Approved, ReviewedAtUtc = now },
                new HunterReviewDecision { HunterSubmissionId = foreignSubmissionId, ReviewerUserId = foreign.UserId, ResultingStatus = HunterSubmissionStatus.Approved, ReviewedAtUtc = now });
            db.Games.AddRange(
                new GameRow { Id = gameId, CreatorUserId = owner.UserId, Title = "Retained archived game", DurationMinutes = 30, Status = GameStatus.Expired, SubmittedAtUtc = now.AddDays(-40), ApprovedAtUtc = now.AddDays(-35), ExpiresAtUtc = now.AddDays(-5) },
                new GameRow { Id = foreignGameId, CreatorUserId = foreign.UserId, Title = "Foreign draft", DurationMinutes = 30, Status = GameStatus.Draft });
            await db.SaveChangesAsync();
        }

        using var submissions = await client.GetAsync("/api/mobile/hunter/submissions");
        Assert.Equal(HttpStatusCode.OK, submissions.StatusCode);
        using var submissionJson = JsonDocument.Parse(await submissions.Content.ReadAsStringAsync());
        Assert.Single(submissionJson.RootElement.EnumerateArray());
        Assert.Equal(submissionId, submissionJson.RootElement[0].GetProperty("submissionId").GetGuid());

        using var places = await client.GetAsync("/api/mobile/hunter/places");
        Assert.Equal(HttpStatusCode.OK, places.StatusCode);
        using var placesJson = JsonDocument.Parse(await places.Content.ReadAsStringAsync());
        Assert.True(placesJson.RootElement.GetArrayLength() >= 2);
        Assert.Contains(placesJson.RootElement.EnumerateArray(), place =>
            place.GetProperty("placeName").GetString() == "Owner place");

        using var games = await client.GetAsync("/api/mobile/games/me");
        Assert.Equal(HttpStatusCode.OK, games.StatusCode);
        using var gamesJson = JsonDocument.Parse(await games.Content.ReadAsStringAsync());
        Assert.Single(gamesJson.RootElement.EnumerateArray());
        Assert.Equal(gameId, gamesJson.RootElement[0].GetProperty("gameId").GetGuid());
        Assert.Equal((int)GameStatus.Expired, gamesJson.RootElement[0].GetProperty("status").GetInt32());
        Assert.False(gamesJson.RootElement[0].TryGetProperty("creatorUserId", out _));

        using var pending = await client.GetAsync("/api/admin/hunter/submissions");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using var pendingJson = JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
        Assert.Contains(pendingJson.RootElement.EnumerateArray(), item =>
            item.GetProperty("submissionId").GetGuid() == pendingSubmissionId);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "user");

        var draftBody = new HunterDraftRequestDto
        {
            PlaceName = "SQL created draft", ClaimedDistrict = "Jaffna",
            Latitude = 9.66, Longitude = 80.00
        };
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var created = await client.PostAsJsonAsync("/api/mobile/hunter/submissions", draftBody);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdDto = await created.Content.ReadFromJsonAsync<Wayhunter.Application.Modules.HunterPlaces.Dtos.HunterSubmissionResponseDto>();
        Assert.NotNull(createdDto);
        using var replay = await client.PostAsJsonAsync("/api/mobile/hunter/submissions", draftBody);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var replayDto = await replay.Content.ReadFromJsonAsync<Wayhunter.Application.Modules.HunterPlaces.Dtos.HunterSubmissionResponseDto>();
        Assert.Equal(createdDto.SubmissionId, replayDto?.SubmissionId);

        draftBody.PlaceName = "SQL updated draft";
        using var updated = await client.PutAsJsonAsync($"/api/mobile/hunter/submissions/{createdDto.SubmissionId:D}", draftBody);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        await using (var verifyScope = app.Services.CreateAsyncScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<WayhunterSharedDbContext>();
            var persisted = await verifyDb.HunterSubmissions.AsNoTracking().SingleAsync(x => x.Id == createdDto.SubmissionId);
            Assert.Equal("SQL updated draft", persisted.PlaceName);
            Assert.Equal(owner.UserId, persisted.UserId);
            Assert.Equal("SQL API owner", persisted.UserNameSnapshot);
            Assert.Single(await verifyDb.HunterRequestIdempotency.Where(x => x.ResourceId == createdDto.SubmissionId).ToArrayAsync());
        }

        using var deniedPending = await client.GetAsync("/api/admin/hunter/submissions");
        Assert.Equal(HttpStatusCode.Forbidden, deniedPending.StatusCode);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "admin");
        using var rejected = await client.PostAsJsonAsync(
            $"/api/admin/hunter/submissions/{pendingSubmissionId:D}/reject",
            new Wayhunter.Api.Controllers.Mobile.HunterPlaces.HunterReviewActionRequest("Evidence does not meet the capture requirements."));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        await using (var verifyReviewScope = app.Services.CreateAsyncScope())
        {
            var verifyDb = verifyReviewScope.ServiceProvider.GetRequiredService<WayhunterSharedDbContext>();
            var decision = await verifyDb.HunterReviewDecisions.AsNoTracking()
                .SingleAsync(x => x.HunterSubmissionId == pendingSubmissionId);
            Assert.Equal(HunterSubmissionStatus.Rejected, decision.ResultingStatus);
            Assert.Equal("Evidence does not meet the capture requirements.", decision.Reason);
        }
        await app.StopAsync();
    }

    private sealed class TestIdentity(UserAccount account) { public UserAccount Account { get; } = account; }

    private sealed class TestAccountReader(UserAccount account) : ITrustedIdentityAccountReader
    {
        public Task<UserAccount?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(account.UserId == userId ? account : null);
    }

    private sealed class SqlHttpTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, identity.Account.UserId) };
            if (Request.Headers.Authorization.ToString().EndsWith("admin", StringComparison.Ordinal))
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
