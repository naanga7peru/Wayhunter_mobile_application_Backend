using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Infrastructure.Integrations.Email;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;
using Xunit.Abstractions;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed class LocalDbAuthenticationPhase3Tests(ITestOutputHelper output)
{
    private const string Server = @"(localdb)\WayhunterTest2026";
    private const string Catalog = "Wayhunter_Identity_Test_20261125";
    private const string ConnectionKey = "ConnectionStrings__WayhunterSharedDb";

    [LocalDbPhase3Fact]
    public async Task VerifyAllAuthenticationCases()
    {
        // Require the process-level override before Program begins registering services.
        var startupConnection = Environment.GetEnvironmentVariable(ConnectionKey);
        AssertTarget(startupConnection);
        var lateConnection = new SqlConnectionStringBuilder(startupConnection)
        {
            ApplicationName = "Wayhunter Phase 3 late configuration probe"
        };

        var capture = new Capture();
        var clock = new TestClock { Offset = TimeSpan.FromSeconds(-61) };
        var jwtSettings = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "phase3-local",
            ["Authentication:Jwt:Audience"] = "phase3-mobile",
            ["Authentication:Jwt:SigningSecret"] = Environment.GetEnvironmentVariable("Authentication__Jwt__SigningSecret"),
            ["Identity:SessionLifetimeMinutes"] = "60",
            ["Identity:PasswordResetLifetimeMinutes"] = "30",
            ["Identity:AdministratorRole"] = "Admin"
        };
        using var root = new WebApplicationFactory<Program>();
        using var factory = root.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(jwtSettings);
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WayhunterSharedDb"] = lateConnection.ConnectionString,
                    ["Identity:EnableSharedAuthentication"] = "true"
                });
            });
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IIdentityOtpEmailSender>();
                services.RemoveAll<IPasswordResetEmailSender>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<IIdentityOtpEmailSender>(capture);
                services.AddSingleton<IPasswordResetEmailSender>(capture);
                services.AddSingleton<TimeProvider>(clock);
            });
        });
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.Equal("GenericWebHostService", Assert.Single(services.GetServices<IHostedService>()).GetType().Name);
        Assert.Same(capture, services.GetRequiredService<IIdentityOtpEmailSender>());
        Assert.Same(capture, services.GetRequiredService<IPasswordResetEmailSender>());
        AssertTarget(services.GetRequiredService<WayhunterSharedDbContext>().Database.GetConnectionString());
        await using var db = await services.GetRequiredService<IDbContextFactory<WayhunterSharedDbContext>>().CreateDbContextAsync();
        AssertTarget(db.Database.GetConnectionString());
        var runtimeConfig = services.GetRequiredService<IConfiguration>().GetConnectionString("WayhunterSharedDb");
        AssertTarget(runtimeConfig);
        Assert.Equal(lateConnection.ApplicationName, new SqlConnectionStringBuilder(runtimeConfig).ApplicationName);
        var efConnection = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        var configuredSql = db.GetService<IDbContextOptions>().Extensions.OfType<RelationalOptionsExtension>().Single().ConnectionString;
        AssertTarget(configuredSql);
        var configured = new SqlConnectionStringBuilder(configuredSql);
        var changedOptions = configured.Keys.Cast<string>()
            .Where(key => !Equals(configured[key], efConnection[key]))
            .ToArray();
        output.WriteLine($"EF options use late configuration marker: {configured.ApplicationName == lateConnection.ApplicationName}. Runtime SQL option differences: {string.Join(", ", changedOptions)}.");
        await db.Database.OpenConnectionAsync();
        await using (var verify = db.Database.GetDbConnection().CreateCommand())
        {
            verify.CommandText = "SELECT CONVERT(int, SERVERPROPERTY('IsLocalDB')), DB_NAME(), CONVERT(varchar(32), SERVERPROPERTY('ProductVersion'))";
            await using var row = await verify.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            Assert.Equal(1, row.GetInt32(0));
            Assert.True(row.GetString(1) == Catalog, "Unexpected runtime SQL catalog.");
            Assert.Equal("17.0.4025.3", row.GetString(2));
        }
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(8, applied.Length);
        Assert.Equal(db.Database.GetMigrations(), applied);
        output.WriteLine($"Runtime SQL isolation verified at {Server}/{Catalog}, version 17.0.4025.3, {applied.Length} migrations.");

        const string adminPath = "/api/admin/hunter/submissions";
        var email = $"phase3-{Guid.NewGuid():N}@example.test";
        var password = Guid.NewGuid().ToString("N") + "A!";
        var replacement = Guid.NewGuid().ToString("N") + "B!";
        var outcomes = new List<bool>();
        void Check(int id, bool data, int[] expected, params int[] actual)
        {
            bool passed = data && expected.SequenceEqual(actual);
            outcomes.Add(passed);
            output.WriteLine($"{id:00} {(passed ? "PASS" : "FAIL")} expected {string.Join("/", expected)}, actual {string.Join("/", actual)}, evidence {data}");
        }
        async Task<int> Post(string path, object payload)
        {
            using var response = await client.PostAsJsonAsync("/" + path, payload);
            return (int)response.StatusCode;
        }
        async Task<(int Status, string? Token)> Login(string candidate)
        {
            using var response = await client.PostAsJsonAsync("/" + IdentityApiContract.LoginRoute, new { email, password = candidate });
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return ((int)response.StatusCode, json!.RootElement.TryGetProperty("accessToken", out var token) ? token.GetString() : null);
        }
        void Bearer(string? token) => client.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        using var swaggerUi = await client.GetAsync("/swagger/index.html");

        var request = new { displayName = "Phase 3", email, password, confirmPassword = password };
        var register = await Post(IdentityApiContract.RegisterRoute, request);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedEmail == email);
        var id = user?.UserId;
        Check(1, user is not null && user.PasswordHash != password && user.VerifiedAtUtc is null, [201], register);
        var duplicate = await Post(IdentityApiContract.RegisterRoute, request);
        Check(2, await db.Users.CountAsync(x => x.NormalizedEmail == email) == 1 && capture.Codes.Count == 1, [409], duplicate);
        var first = capture.Codes.FirstOrDefault() ?? "";
        Check(3, first.Length == 6 && await db.IdentityOtpChallenges.AnyAsync(x => x.UserId == id && x.OtpVerifierHash != first), [201], register);
        var cooldown = await Post(IdentityApiContract.ResendOtpRoute, new { email });
        clock.Offset = TimeSpan.Zero;
        var resend = await Post(IdentityApiContract.ResendOtpRoute, new { email });
        var latest = capture.Codes.LastOrDefault() ?? "";
        var stale = await Post(IdentityApiContract.VerifyOtpRoute, new { email, code = first });
        Check(4, capture.Codes.Count == 2 && await db.IdentityOtpChallenges.CountAsync(x => x.UserId == id) == 2, [429, 202, 400], cooldown, resend, stale);
        var invalid = await Post(IdentityApiContract.VerifyOtpRoute, new { email, code = latest == "000000" ? "000001" : "000000" });
        Check(5, await db.IdentityOtpChallenges.AnyAsync(x => x.UserId == id && x.FailedAttemptCount > 0), [400], invalid);
        var verified = await Post(IdentityApiContract.VerifyOtpRoute, new { email, code = latest });
        Check(6, await db.Users.AnyAsync(x => x.UserId == id && x.VerifiedAtUtc != null) && await db.IdentityOtpChallenges.AnyAsync(x => x.UserId == id && x.ConsumedAtUtc != null), [204], verified);
        var badLogin = await Login(password + "wrong");
        Check(7, !await db.IdentitySessions.AnyAsync(x => x.UserId == id), [401], badLogin.Status);
        var login = await Login(password);
        Check(8, await db.IdentitySessions.CountAsync(x => x.UserId == id) == 1, [200], login.Status);

        var handler = new JwtSecurityTokenHandler();
        var jwt = login.Token is not null && handler.CanReadToken(login.Token) ? handler.ReadJwtToken(login.Token) : null;
        Check(9, jwt is not null && jwt.Subject == id && jwt.Issuer == jwtSettings["Authentication:Jwt:Issuer"] &&
            jwt.Audiences.Contains(jwtSettings["Authentication:Jwt:Audience"]!) && jwt.ValidTo > DateTime.UtcNow, [200], login.Status);
        var profilePath = "/" + IdentityApiContract.MyProfileRoute;
        int anonymous = (int)(await client.GetAsync(profilePath)).StatusCode;
        if (login.Token is { } token)
        {
            var parts = token.Split('.');
            if (parts.Length == 3 && parts[2].Length > 0)
                Bearer(parts[0] + "." + parts[1] + "." + (parts[2][0] == 'A' ? 'B' : 'A') + parts[2][1..]);
        }
        int invalidSignature = (int)(await client.GetAsync(profilePath)).StatusCode;
        Bearer(login.Token);
        using var profile = await client.GetAsync(profilePath);
        bool swaggerBearer = false;
        if (swagger.IsSuccessStatusCode)
        {
            using var document = (await swagger.Content.ReadFromJsonAsync<JsonDocument>())!;
            var components = document.RootElement.GetProperty("components");
            var paths = document.RootElement.GetProperty("paths");
            swaggerBearer = components.TryGetProperty("securitySchemes", out var schemes) &&
                schemes.TryGetProperty("Bearer", out var bearer) && bearer.GetProperty("scheme").GetString() == "bearer" &&
                paths.GetProperty(profilePath).GetProperty("get").TryGetProperty("security", out var security) &&
                security.EnumerateArray().Any(x => x.TryGetProperty("Bearer", out _)) &&
                !paths.GetProperty("/" + IdentityApiContract.LoginRoute).GetProperty("post").TryGetProperty("security", out _);
        }
        Check(10, swaggerBearer, [200, 200, 401, 401, 200], (int)swaggerUi.StatusCode, (int)swagger.StatusCode, anonymous, invalidSignature, (int)profile.StatusCode);
        using var profileJson = await profile.Content.ReadFromJsonAsync<JsonDocument>();
        Check(11, profile.IsSuccessStatusCode && profileJson!.RootElement.GetProperty("userId").GetString() == id &&
            profileJson.RootElement.GetProperty("email").GetString() == email, [200], (int)profile.StatusCode);

        Bearer(null);
        int anonymousAdmin = (int)(await client.GetAsync(adminPath)).StatusCode;
        Bearer(login.Token);
        int regularAdmin = (int)(await client.GetAsync(adminPath)).StatusCode;
        if (id is not null)
        {
            db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(id, DateTimeOffset.UtcNow, null));
            await db.SaveChangesAsync();
        }
        Bearer(null);
        var adminLogin = await Login(password);
        Bearer(adminLogin.Token);
        int allowedAdmin = (int)(await client.GetAsync(adminPath)).StatusCode;
        Check(12, await db.UserRoleAssignments.AnyAsync(x => x.UserId == id && x.Role == "Admin"), [401, 403, 200, 200], anonymousAdmin, regularAdmin, adminLogin.Status, allowedAdmin);

        Bearer(null);
        var forgot = await Post(IdentityApiContract.ForgotPasswordRoute, new { email });
        var resetToken = capture.ResetToken;
        Check(13, resetToken is not null && await db.IdentityPasswordResetCredentials.AnyAsync(x => x.UserId == id && x.TokenHash != resetToken), [202], forgot);
        var resetRequest = new { email, token = resetToken ?? "", newPassword = replacement, confirmPassword = replacement };
        var reset = await Post(IdentityApiContract.ResetPasswordRoute, resetRequest);
        var replay = await Post(IdentityApiContract.ResetPasswordRoute, resetRequest);
        var oldLogin = await Login(password);
        var newLogin = await Login(replacement);
        Bearer(adminLogin.Token);
        int resetRevoked = (int)(await client.GetAsync(profilePath)).StatusCode;
        Check(14, await db.IdentityPasswordResetCredentials.AnyAsync(x => x.UserId == id && x.ConsumedAtUtc != null), [204, 400, 401, 200, 401], reset, replay, oldLogin.Status, newLogin.Status, resetRevoked);
        Bearer(newLogin.Token);
        int before = await db.IdentitySessions.CountAsync(x => x.UserId == id && x.RevokedAtUtc != null);
        int logout = (int)(await client.PostAsync("/" + IdentityApiContract.LogoutRoute, null)).StatusCode;
        Check(15, await db.IdentitySessions.CountAsync(x => x.UserId == id && x.RevokedAtUtc != null) == before + 1, [204], logout);
        Check(16, newLogin.Token is not null, [401], (int)(await client.GetAsync(profilePath)).StatusCode);
        Assert.True(outcomes.Count == 16 && outcomes.All(result => result), "See individual sanitized case results.");
    }

    private static void AssertTarget(string? raw)
    {
        SqlConnectionStringBuilder? target;
        try { target = raw is null ? null : new SqlConnectionStringBuilder(raw); }
        catch (ArgumentException) { target = null; }
        Assert.True(target is not null &&
            string.Equals(target.DataSource, Server, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(target.InitialCatalog, Catalog, StringComparison.Ordinal) &&
            target.IntegratedSecurity && target.Encrypt == SqlConnectionEncryptOption.Mandatory &&
            target.TrustServerCertificate, "SQL connection failed the dedicated LocalDB allowlist.");
    }

    private sealed class TestClock : TimeProvider
    {
        public TimeSpan Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
    }

    private sealed class Capture : IIdentityOtpEmailSender, IPasswordResetEmailSender
    {
        public List<string> Codes { get; } = [];
        public string? ResetToken { get; private set; }

        public Task<bool> SendAsync(string email, OtpEmailContent content, CancellationToken cancellationToken)
        {
            var match = Regex.Match(content.TextBody, @"verification code is: (\d{6})");
            if (match.Success)
                Codes.Add(match.Groups[1].Value);
            return Task.FromResult(match.Success);
        }

        public Task<bool> SendAsync(string email, string displayName, string resetToken, int expiryMinutes, CancellationToken cancellationToken)
        {
            ResetToken = resetToken;
            return Task.FromResult(true);
        }
    }
}

public sealed class LocalDbPhase3FactAttribute : FactAttribute
{
    public LocalDbPhase3FactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_LOCAL_IDENTITY_PHASE3") != "1")
            Skip = "Opt in with WAYHUNTER_LOCAL_IDENTITY_PHASE3=1 and the dedicated LocalDB connection override.";
    }
}
