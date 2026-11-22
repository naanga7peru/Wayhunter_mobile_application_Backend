using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Controllers.Admin.HomeAdsAnalytics;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class M1M7RealBearerAdvertisementHttpTests
{
    private const string Route = "/api/admin/advertisements";

    private const string Secret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private sealed record TestState(
        UserAccount Account,
        IdentitySession Session,
        SignedSessionTokenService Tokens,
        string Token);

    private sealed class FakeSessionReader
        : IAuthenticatedSessionReader
    {
        public UserAccount? Account { get; init; }
        public IdentitySession? Session { get; init; }

        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Account);
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Session);
        }
    }

    // Any repository access means authorization was bypassed.
    // No SQL database or advertisement writes are used.
    private sealed class ForbiddenRepository :
        IAdvertisementRepository,
        IAdvertisementWriteRepository
    {
        public Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
            DateTimeOffset nowUtc,
            AdvertisementPlacement? placement = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Unauthorized request reached advertisement repository.");

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Unauthorized request reached advertisement repository.");

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Unauthorized request reached advertisement repository.");

        public Task<AdvertisementDto> CreateAsync(
            AdvertisementCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Unauthorized request reached advertisement repository.");

        public Task<AdvertisementDto?> UpdateAsync(
            Guid id,
            AdvertisementUpdateRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Unauthorized request reached advertisement repository.");

        public Task<AdvertisementDto?> ChangeStatusAsync(
            Guid id,
            AdvertisementStatusUpdateRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Unauthorized request reached advertisement repository.");
    }

    private static async Task<WebApplication> CreateServerAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var account = UserAccount.RegisterPending(
            "M1 M7 HTTP User",
            "m1-m7-http@example.com",
            "test-password-hash",
            now.AddHours(-2));

        account.ActivateAfterVerifiedOtp(
            now.AddHours(-1));

        var session = IdentitySession.Create(
            account.UserId,
            now.AddMinutes(-5),
            now.AddMinutes(30));

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });

        builder.WebHost.UseTestServer();

        builder.Configuration["Authentication:Jwt:Issuer"] =
            "wayhunter-test";

        builder.Configuration["Authentication:Jwt:Audience"] =
            "wayhunter-mobile-test";

        builder.Configuration["Authentication:Jwt:SigningSecret"] =
            Secret;

        builder.Configuration["Identity:AdministratorRole"] =
            "Admin";

        builder.Services
            .AddControllers()
            .AddApplicationPart(
                typeof(AdvertisementsController).Assembly);

        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddSingleton<IAuthenticatedSessionReader>(
            new FakeSessionReader
            {
                Account = account,
                Session = session
            });

        // Actual M1 session authentication registration.
        builder.Services.AddWayhunterSessionAuthentication(
            builder.Configuration);

        // Actual M7 Administrator authorization policy.
        builder.Services.AddAdvertisementAdministratorPolicy(
            builder.Configuration);

        builder.Services.AddSingleton<ForbiddenRepository>();

        builder.Services.AddScoped<IAdvertisementRepository>(
            sp => sp.GetRequiredService<ForbiddenRepository>());

        builder.Services.AddScoped<IAdvertisementWriteRepository>(
            sp => sp.GetRequiredService<ForbiddenRepository>());

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();

        var tokens = app.Services
            .GetRequiredService<SignedSessionTokenService>();

        var token = tokens.Issue(account, session, now);

        // Keep isolated test identity state accessible to tests.
        // No production persistence or roles are registered.
        TestStates.Add(app, new TestState(
            account, session, tokens, token));

        return app;
    }

    private static readonly
        System.Runtime.CompilerServices.ConditionalWeakTable<
            WebApplication, TestState> TestStates = new();

    private static TestState GetState(WebApplication app)
        => TestStates.GetValue(
            app,
            _ => throw new InvalidOperationException(
                "HTTP test state missing."));

    private static string CreateSignedJwtWithAdminClaim(
        TestState state)
    {
        var now = DateTimeOffset.UtcNow;

        var jwt = new JwtSecurityToken(
            issuer: "wayhunter-test",
            audience: "wayhunter-mobile-test",
            claims:
            [
                new Claim("sub", state.Account.UserId),
                new Claim("sid", state.Session.SessionId),
                new Claim("jti", Guid.NewGuid().ToString("D")),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            notBefore: now.UtcDateTime.AddMinutes(-1),
            expires: state.Session.ExpiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("invalid", HttpStatusCode.Unauthorized)]
    [InlineData("valid-non-admin", HttpStatusCode.Forbidden)]
    [InlineData("injected-admin", HttpStatusCode.Forbidden)]
    [InlineData("revoked", HttpStatusCode.Unauthorized)]
    [InlineData("suspended", HttpStatusCode.Unauthorized)]
    [InlineData("deletion-requested", HttpStatusCode.Unauthorized)]
    public async Task AdvertisementGet_EnforcesM1M7SecurityContract(
        string scenario,
        HttpStatusCode expectedStatus)
    {
        await using var app = await CreateServerAsync();
        using var client = app.GetTestClient();

        var state = GetState(app);

        string? token = scenario switch
        {
            "missing" => null,
            "invalid" => "not.a.valid.jwt",
            "injected-admin" =>
                CreateSignedJwtWithAdminClaim(state),
            _ => state.Token
        };

        if (scenario == "injected-admin")
        {
            // Prove the specially crafted test JWT is
            // cryptographically valid and contains Admin.
            var verified = state.Tokens.Validate(token!);

            Assert.Contains(
                verified.Claims,
                claim => claim.Type == ClaimTypes.Role &&
                         claim.Value == "Admin");
        }

        if (scenario == "revoked")
        {
            state.Session.Revoke(DateTimeOffset.UtcNow);
        }

        if (scenario == "suspended")
        {
            state.Account.Suspend();
        }

        if (scenario == "deletion-requested")
        {
            state.Account.RequestDeletion(
                DateTimeOffset.UtcNow);
        }

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", token);
        }

        using var response = await client.GetAsync(Route);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task ValidM1NonAdminBearer_CannotCreateAdvertisement()
    {
        await using var app = await CreateServerAsync();
        using var client = app.GetTestClient();

        var state = GetState(app);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                state.Token);

        var now = DateTimeOffset.UtcNow;

        var request = new AdvertisementCreateRequest
        {
            Title = "Unauthorized advertisement",
            ImageUrl = "https://example.com/banner.jpg",
            StartAtUtc = now.AddDays(1),
            EndAtUtc = now.AddDays(2),
            Placement = AdvertisementPlacement.HomeBanner
        };

        using var response =
            await client.PostAsJsonAsync(Route, request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    public async Task AdminWriteEndpoints_RejectUntrustedBearer(
        string verb,
        bool injectedAdminRole)
    {
        await using var app = await CreateServerAsync();
        using var client = app.GetTestClient();

        var state = GetState(app);

        var token = injectedAdminRole
            ? CreateSignedJwtWithAdminClaim(state)
            : state.Token;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var advertisementId = Guid.NewGuid();

        var url = verb switch
        {
            "POST" => Route,
            "PUT" => $"{Route}/{advertisementId}",
            "PATCH" => $"{Route}/{advertisementId}/status",
            _ => throw new ArgumentOutOfRangeException(
                nameof(verb))
        };

        var method = verb switch
        {
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "PATCH" => HttpMethod.Patch,
            _ => throw new ArgumentOutOfRangeException(
                nameof(verb))
        };

        using var request = new HttpRequestMessage(
            method, url);

        var now = DateTimeOffset.UtcNow;

        request.Content = JsonContent.Create(new
        {
            Title = "Unauthorized change",
            ImageUrl = "https://example.com/banner.jpg",
            StartAtUtc = now.AddDays(1),
            EndAtUtc = now.AddDays(2),
            Placement = AdvertisementPlacement.HomeBanner,
            Status = AdvertisementStatus.Active
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("suspended")]
    [InlineData("deletion-requested")]
    public async Task SessionStateChange_RevokesHttpAccess(
        string scenario)
    {
        await using var app = await CreateServerAsync();
        using var client = app.GetTestClient();

        var state = GetState(app);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                state.Token);

        // Initially valid user session, but not Admin.
        using (var initial = await client.GetAsync(Route))
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                initial.StatusCode);
        }

        switch (scenario)
        {
            case "revoked":
                state.Session.Revoke(
                    DateTimeOffset.UtcNow);
                break;

            case "suspended":
                state.Account.Suspend();
                break;

            case "deletion-requested":
                state.Account.RequestDeletion(
                    DateTimeOffset.UtcNow);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario));
        }

        // Reusing the same previously valid JWT must fail.
        using var response = await client.GetAsync(Route);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
