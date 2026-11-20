using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Controllers.Mobile.IdentitySocial;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed class MyProfileHttpTests
{
    private const string SigningSecret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private sealed class TestSessionReader(
        UserAccount account,
        IdentitySession session) : IAuthenticatedSessionReader
    {
        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<UserAccount?>(
                account.UserId == userId ? account : null);
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IdentitySession?>(
                session.SessionId == sessionId ? session : null);
        }
    }

    [Fact]
    public async Task MyProfile_RequiresBearerAndReturnsOwnerProfile()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.RegisterPending(
            "HTTP Owner",
            "http-owner@example.test",
            "test-only-password-hash",
            now.AddMinutes(-10));
        account.ActivateAfterVerifiedOtp(now.AddMinutes(-9));
        var session = IdentitySession.Create(
            account.UserId,
            now.AddMinutes(-1),
            now.AddMinutes(20));
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration["Authentication:Jwt:Issuer"] = "m1-http-test";
        builder.Configuration["Authentication:Jwt:Audience"] = "mobile-test";
        builder.Configuration["Authentication:Jwt:SigningSecret"] = SigningSecret;
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(MyProfileController).Assembly);
        builder.Services.AddDbContext<WayhunterSharedDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton<IAuthenticatedSessionReader>(
            new TestSessionReader(account, session));
        builder.Services.AddWayhunterSessionAuthentication(
            builder.Configuration);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider
                .GetRequiredService<WayhunterSharedDbContext>();
            db.Users.Add(account);
            db.UserProfiles.Add(UserProfile.CreateForUser(account, now));
            await db.SaveChangesAsync();
        }

        using var client = app.GetTestClient();
        var anonymous = await client.GetAsync("/api/mobile/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var tokenService = app.Services
            .GetRequiredService<SignedSessionTokenService>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", tokenService.Issue(account, session, now));

        var response = await client.GetAsync("/api/mobile/identity/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content
            .ReadFromJsonAsync<PrivateProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal(account.UserId, profile.UserId);
        Assert.Equal("HTTP Owner", profile.DisplayName);
        Assert.Equal("http-owner@example.test", profile.Email);
    }
}
