using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Controllers.Mobile.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

public sealed class M4DisposableSqlFactAttribute : FactAttribute
{
    public M4DisposableSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_M4_RUN_DISPOSABLE_SQL_TESTS") != "1")
            Skip = "Set WAYHUNTER_M4_RUN_DISPOSABLE_SQL_TESTS=1 only when the MSSQLLocalDB instance is available.";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M4_TEST_SQL_CONNECTION")))
            Skip = "Configure WAYHUNTER_M4_TEST_SQL_CONNECTION to one uniquely named disposable M4 LocalDB catalog.";
    }
}

public sealed class M4DisposableSqlDraftHttpTests
{
    [M4DisposableSqlFact]
    public async Task MigratedDisposableSqlDatabaseSupportsAuthenticatedDraftHttpAndIdempotency()
    {
        var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M4_TEST_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var sqlBuilder = new SqlConnectionStringBuilder(connection);
        Assert.Matches(@"^Wayhunter_Test_M4_[0-9a-fA-F]{32}$", sqlBuilder.InitialCatalog);
        Assert.Equal(@"(localdb)\MSSQLLocalDB", sqlBuilder.DataSource, ignoreCase: true);
        Assert.False(string.Equals("Wayhunter_Official", sqlBuilder.InitialCatalog, StringComparison.OrdinalIgnoreCase));
        WebApplication? app = null;
        try
        {
            var dbOptions = new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseSqlServer(connection).Options;
            await using (var db = new WayhunterSharedDbContext(dbOptions))
            {
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.Contains("20261123110145_M4HunterWriteWorkflow", await db.Database.GetAppliedMigrationsAsync());
            }

            var now = DateTimeOffset.UtcNow;
            var owner = UserAccount.RegisterPending("M4 disposable owner",
                $"m4-{Guid.NewGuid():N}@example.test", "test-only-hash", now.AddMinutes(-10));
            owner.ActivateAfterVerifiedOtp(now.AddMinutes(-9));
            var foreign = UserAccount.RegisterPending("Foreign disposable owner",
                $"m4-foreign-{Guid.NewGuid():N}@example.test", "test-only-hash", now.AddMinutes(-10));
            foreign.ActivateAfterVerifiedOtp(now.AddMinutes(-9));
            await using (var db = new WayhunterSharedDbContext(dbOptions))
            {
                db.Users.AddRange(owner, foreign);
                await db.SaveChangesAsync();
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Configuration["Identity:AdministratorRole"] = "Admin";
            builder.Services.AddControllers().AddApplicationPart(typeof(HunterDraftsController).Assembly);
            builder.Services.AddDbContext<WayhunterSharedDbContext>(options => options.UseSqlServer(connection));
            builder.Services.AddScoped<ITrustedIdentityAccountReader, SqlTrustedIdentityAccountReader>();
            builder.Services.AddScoped<IHunterDraftStore, SqlHunterDraftStore>();
            builder.Services.AddScoped<HunterDraftService>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddAuthentication("M4DisposableSql")
                .AddScheme<AuthenticationSchemeOptions, DisposableSqlAuthHandler>("M4DisposableSql", _ => { });
            builder.Services.AddCoinsWalletAdministratorPolicy(builder.Configuration);

            app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.UserId);
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            var key = client.DefaultRequestHeaders.GetValues("Idempotency-Key").Single();
            var input = new HunterDraftRequestDto
            {
                PlaceName = "Disposable SQL Draft", ClaimedDistrict = "Jaffna",
                Latitude = 9.66, Longitude = 80.0
            };
            var concurrent = await Task.WhenAll(
                client.PostAsJsonAsync("/api/mobile/hunter/submissions", input),
                client.PostAsJsonAsync("/api/mobile/hunter/submissions", input));
            Assert.All(concurrent, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var firstResult = await concurrent[0].Content.ReadFromJsonAsync<HunterSubmissionResponseDto>();
            var secondResult = await concurrent[1].Content.ReadFromJsonAsync<HunterSubmissionResponseDto>();
            Assert.NotNull(firstResult);
            Assert.Equal(firstResult.SubmissionId, secondResult?.SubmissionId);
            var response = firstResult;

            using var replay = await client.PostAsJsonAsync("/api/mobile/hunter/submissions", input);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            var replayResult = await replay.Content.ReadFromJsonAsync<HunterSubmissionResponseDto>();
            Assert.Equal(response.SubmissionId, replayResult?.SubmissionId);

            input.PlaceName = "Updated SQL Draft";
            using var updated = await client.PutAsJsonAsync($"/api/mobile/hunter/submissions/{response.SubmissionId:D}", input);
            Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", foreign.UserId);
            using var foreignUpdate = await client.PutAsJsonAsync($"/api/mobile/hunter/submissions/{response.SubmissionId:D}", input);
            Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);

            await using (var verify = new WayhunterSharedDbContext(dbOptions))
            {
                var row = await verify.HunterSubmissions.AsNoTracking().SingleAsync(x => x.Id == response.SubmissionId);
                Assert.Equal(owner.UserId, row.UserId);
                Assert.Equal("M4 disposable owner", row.UserNameSnapshot);
                Assert.Equal("Updated SQL Draft", row.PlaceName);
                var marker = await verify.HunterRequestIdempotency.AsNoTracking()
                    .SingleAsync(x => x.OwnerUserId == owner.UserId && x.Operation == "draft.create");
                Assert.Equal(response.SubmissionId, marker.ResourceId);
                Assert.Equal(64, marker.KeyHash.Length);
                Assert.NotEqual(key, marker.KeyHash);
            }
            await app.StopAsync();
        }
        finally
        {
            if (app is not null) await app.DisposeAsync();
        }
    }

    private sealed class DisposableSqlAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParseExact(Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.Ordinal), "D", out var owner))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, owner.ToString("D"))], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
