using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Api.Controllers.Mobile.IdentitySocial;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Integrations.Email;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed class IdentityAuthenticationFlowHttpTests
{
    private const string SigningSecret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private sealed class TestEmailSender : IIdentityOtpEmailSender
    {
        public List<OtpEmailContent> Sent { get; } = [];

        public Task<bool> SendAsync(
            string recipientEmail,
            OtpEmailContent content,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sent.Add(content);
            return Task.FromResult(true);
        }

        public string LatestCode
        {
            get
            {
                var match = Regex.Match(
                    Sent[^1].TextBody,
                    @"verification code is: (\d{6})");
                Assert.True(match.Success, "Test email must contain the generated OTP.");
                return match.Groups[1].Value;
            }
        }
    }

    private sealed class TestOtpCodeGenerator : IIdentityOtpCodeGenerator
    {
        private int _next;
        public string Generate() => (++_next).ToString("D6");
    }

    private sealed class InMemorySessionRevocationStore(
        WayhunterSharedDbContext db) : IIdentitySessionRevocationStore
    {
        public Task<IdentitySession?> FindAsync(
            string sessionId,
            CancellationToken cancellationToken = default) =>
            db.IdentitySessions.SingleOrDefaultAsync(
                x => x.SessionId == sessionId, cancellationToken);

        public async Task<SessionRevocationWriteResult> TryRevokeAsync(
            string userId,
            string sessionId,
            DateTimeOffset revokedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var session = await db.IdentitySessions.SingleOrDefaultAsync(
                x => x.SessionId == sessionId && x.UserId == userId,
                cancellationToken);
            if (session is null)
                return SessionRevocationWriteResult.NotFound;
            if (session.RevokedAtUtc.HasValue)
                return SessionRevocationWriteResult.AlreadyRevoked;
            session.Revoke(revokedAtUtc);
            await db.SaveChangesAsync(cancellationToken);
            return SessionRevocationWriteResult.Revoked;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    [Fact]
    public async Task RegisterVerifyLoginProfileLogout_RevokedTokenIsRejected()
    {
        var emailSender = new TestEmailSender();
        await using var app = await CreateServerAsync(emailSender);
        using var client = app.GetTestClient();

        var email = $"flow-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.RegisterRoute,
            new IdentityRegisterRequest(
                "Flow User", email, "A-strong-test-password", "A-strong-test-password"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = await register.Content
            .ReadFromJsonAsync<RegistrationIdentityResponseDto>();
        Assert.NotNull(registered);
        Assert.True(registered.RequiresOtpVerification);

        var duplicateRegistration = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.RegisterRoute,
            new IdentityRegisterRequest(
                "Duplicate", email, "A-strong-test-password", "A-strong-test-password"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateRegistration.StatusCode);
        Assert.Single(emailSender.Sent);

        var beforeActivation = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.LoginRoute,
            new IdentitySignInRequest(email, "A-strong-test-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, beforeActivation.StatusCode);

        var verify = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.VerifyOtpRoute,
            new IdentityOtpRequest(email, emailSender.LatestCode));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.LoginRoute,
            new IdentitySignInRequest(email, "A-strong-test-password"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<IdentitySignInResult>();
        Assert.NotNull(session);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var profile = await client.GetAsync("/" + IdentityApiContract.MyProfileRoute);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var privateProfile = await profile.Content.ReadFromJsonAsync<PrivateProfileDto>();
        Assert.NotNull(privateProfile);
        Assert.Equal(registered.UserId, privateProfile.UserId);
        Assert.Equal(email, privateProfile.Email);

        var logout = await client.PostAsync("/api/mobile/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var reusedToken = await client.GetAsync("/" + IdentityApiContract.MyProfileRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedToken.StatusCode);
    }

    [Fact]
    public async Task ResendCooldownAndOtpAttemptLimit_AreEnforcedOverHttp()
    {
        var emailSender = new TestEmailSender();
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var app = await CreateServerAsync(emailSender, clock);
        using var client = app.GetTestClient();
        var email = $"otp-{Guid.NewGuid():N}@example.test";

        var register = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.RegisterRoute,
            new IdentityRegisterRequest(
                "OTP User", email, "A-strong-test-password", "A-strong-test-password"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var firstCode = emailSender.LatestCode;

        var resend = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.ResendOtpRoute,
            new IdentityOtpResendRequest(email));
        Assert.Equal(HttpStatusCode.TooManyRequests, resend.StatusCode);
        Assert.Equal("60", resend.Headers.RetryAfter?.Delta?.TotalSeconds.ToString("0"));

        clock.Advance(TimeSpan.FromSeconds(60));
        var resendAfterCooldown = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.ResendOtpRoute,
            new IdentityOtpResendRequest(email));
        Assert.Equal(HttpStatusCode.Accepted, resendAfterCooldown.StatusCode);
        Assert.Equal(2, emailSender.Sent.Count);
        Assert.NotEqual(firstCode, emailSender.LatestCode);

        var replacedCode = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.VerifyOtpRoute,
            new IdentityOtpRequest(email, firstCode));
        Assert.Equal(HttpStatusCode.BadRequest, replacedCode.StatusCode);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await client.PostAsJsonAsync(
                "/" + IdentityApiContract.VerifyOtpRoute,
                new IdentityOtpRequest(email, "000000"));
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        var correctAfterLimit = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.VerifyOtpRoute,
            new IdentityOtpRequest(email, emailSender.LatestCode));
        Assert.Equal(HttpStatusCode.BadRequest, correctAfterLimit.StatusCode);
    }

    [Fact]
    public async Task ExpiredOtp_IsRejectedOverHttp()
    {
        var emailSender = new TestEmailSender();
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var app = await CreateServerAsync(emailSender, clock);
        using var client = app.GetTestClient();
        var email = $"expired-{Guid.NewGuid():N}@example.test";

        var register = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.RegisterRoute,
            new IdentityRegisterRequest(
                "Expired User", email, "A-strong-test-password", "A-strong-test-password"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var code = emailSender.LatestCode;
        clock.Advance(TimeSpan.FromMinutes(5));

        var verify = await client.PostAsJsonAsync(
            "/" + IdentityApiContract.VerifyOtpRoute,
            new IdentityOtpRequest(email, code));
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }

    private static async Task<WebApplication> CreateServerAsync(
        TestEmailSender emailSender,
        ManualTimeProvider? clock = null)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration["Identity:SessionLifetimeMinutes"] = "60";
        builder.Configuration["Authentication:Jwt:Issuer"] = "m1-flow-test";
        builder.Configuration["Authentication:Jwt:Audience"] = "mobile-test";
        builder.Configuration["Authentication:Jwt:SigningSecret"] = SigningSecret;

        var databaseName = Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<WayhunterSharedDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton<TimeProvider>(clock ?? TimeProvider.System);
        builder.Services.AddWayhunterIdentityPersistence(builder.Configuration);
        builder.Services.AddWayhunterSessionAuthentication(builder.Configuration);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(IdentityAuthenticationController).Assembly);
        builder.Services.RemoveAll<IIdentityOtpEmailSender>();
        builder.Services.AddSingleton<IIdentityOtpEmailSender>(emailSender);
        builder.Services.RemoveAll<IIdentityOtpCodeGenerator>();
        builder.Services.AddSingleton<IIdentityOtpCodeGenerator, TestOtpCodeGenerator>();
        builder.Services.RemoveAll<IIdentitySessionRevocationStore>();
        builder.Services.AddScoped<IIdentitySessionRevocationStore>(sp =>
            new InMemorySessionRevocationStore(
                sp.GetRequiredService<WayhunterSharedDbContext>()));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }
}
