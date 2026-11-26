using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class M7SqlAdvertisementHttpFactAttribute : FactAttribute
{
    public M7SqlAdvertisementHttpFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M7_TEST_SQL_CONNECTION")))
            Skip = "Set the isolated M7 SQL connection to run advertisement HTTP-to-SQL acceptance.";
    }
}

public sealed class M7SqlAdvertisementHttpE2ETests
{
    private const string Issuer = "wayhunter-m7-test";
    private const string Audience = "wayhunter-mobile-test";
    private const string Secret = "test-only-32-byte-or-longer-jwt-signing-key-123";

    [M7SqlAdvertisementHttpFact]
    public async Task AdminCrudAndActiveListing_PersistAndFilterByPlacementAndWindow()
    {
        var connection = GetConnectionString();
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid().ToString("N");
        var admin = UserAccount.RegisterPending("M7 SQL admin", $"m7-{Guid.NewGuid():N}@example.invalid", "test-only-password-hash", now.AddMinutes(-2));
        admin.ActivateAfterVerifiedOtp(now.AddMinutes(-1));
        var session = IdentitySession.Create(admin.UserId, now.AddSeconds(-30), now.AddHours(1));
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseSqlServer(connection).Options;
        await using (var db = new WayhunterSharedDbContext(options))
        {
            db.Users.Add(admin);
            db.IdentitySessions.Add(session);
            db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(admin.UserId, now, null));
            await db.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:WayhunterSharedDb", connection);
            builder.UseSetting("Identity:EnableSharedAuthentication", "true");
            builder.UseSetting("Identity:AdministratorRole", "Admin");
            builder.UseSetting("Authentication:Jwt:Issuer", Issuer);
            builder.UseSetting("Authentication:Jwt:Audience", Audience);
            builder.UseSetting("Authentication:Jwt:SigningSecret", Secret);
            builder.UseSetting("Advertisements:EnableSqlPersistence", "true");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WayhunterSharedDb"] = connection,
                ["Identity:EnableSharedAuthentication"] = "true",
                ["Identity:AdministratorRole"] = "Admin",
                ["Authentication:Jwt:Issuer"] = Issuer,
                ["Authentication:Jwt:Audience"] = Audience,
                ["Authentication:Jwt:SigningSecret"] = Secret,
                ["Advertisements:EnableSqlPersistence"] = "true"
            }));
        });

        using var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<SignedSessionTokenService>().Issue(admin, session, now);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var anonymousClient = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
            Assert.IsType<AdvertisementRepository>(scope.ServiceProvider.GetRequiredService<Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces.IAdvertisementRepository>());

        var current = new AdvertisementCreateRequest
        {
            Title = $"M7 SQL {runId} current banner", ImageUrl = "https://example.invalid/demo-image.png",
            DestinationUrl = "https://example.invalid/demo", StartAtUtc = now.AddHours(-1), EndAtUtc = now.AddDays(1),
            Placement = AdvertisementPlacement.HomeBanner
        };
        using var createdResponse = await client.PostAsJsonAsync("/api/admin/advertisements", current);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<AdvertisementDto>();
        Assert.NotNull(created);
        using (var invalidCreate = await client.PostAsJsonAsync("/api/admin/advertisements", current with { Title = "" }))
            Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        using (var emptyId = await client.GetAsync("/api/admin/advertisements/00000000-0000-0000-0000-000000000000"))
            Assert.Equal(HttpStatusCode.BadRequest, emptyId.StatusCode);
        using (var missingId = await client.GetAsync($"/api/admin/advertisements/{Guid.NewGuid()}"))
            Assert.Equal(HttpStatusCode.NotFound, missingId.StatusCode);
        using (var list = await client.GetAsync("/api/admin/advertisements"))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var listed = await list.Content.ReadFromJsonAsync<AdvertisementDto[]>();
            Assert.NotNull(listed);
            Assert.Contains(listed, item => item.Id == created.Id);
        }
        using (var invalidUpdate = await client.PutAsJsonAsync($"/api/admin/advertisements/{created.Id}", current with { EndAtUtc = current.StartAtUtc }))
            Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        var updateRequest = new AdvertisementUpdateRequest
        {
            Title = $"M7 SQL {runId} updated banner", ImageUrl = current.ImageUrl,
            DestinationUrl = current.DestinationUrl, StartAtUtc = current.StartAtUtc,
            EndAtUtc = current.EndAtUtc, Placement = current.Placement
        };
        using (var update = await client.PutAsJsonAsync($"/api/admin/advertisements/{created.Id}", updateRequest))
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using (var missingUpdate = await client.PutAsJsonAsync($"/api/admin/advertisements/{Guid.NewGuid()}", updateRequest))
            Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
        using (var invalidStatus = await client.PatchAsJsonAsync($"/api/admin/advertisements/{created.Id}/status", new AdvertisementStatusUpdateRequest { Status = (AdvertisementStatus)999 }))
            Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
        using (var anonymousDenied = await anonymousClient.GetAsync("/api/admin/advertisements"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousDenied.StatusCode);
        using (var invalidJwt = factory.CreateClient())
        {
            invalidJwt.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");
            using var denied = await invalidJwt.GetAsync("/api/admin/advertisements");
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using (var imageForm = new MultipartFormDataContent())
        using (var imageFile = new ByteArrayContent(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            imageFile.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            imageForm.Add(imageFile, "file", "fixture.png");
            using var disabledImage = await client.PostAsync($"/api/admin/advertisements/{created.Id}/image", imageForm);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, disabledImage.StatusCode);
        }
        using var activeResponse = await client.PatchAsJsonAsync($"/api/admin/advertisements/{created.Id}/status", new AdvertisementStatusUpdateRequest { Status = AdvertisementStatus.Active });
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);

        var future = new AdvertisementCreateRequest
        {
            Title = $"M7 SQL {runId} future banner", ImageUrl = "https://example.invalid/future-image.png",
            StartAtUtc = now.AddDays(1), EndAtUtc = now.AddDays(2), Placement = AdvertisementPlacement.HomeBanner
        };
        using var futureCreate = await client.PostAsJsonAsync("/api/admin/advertisements", future);
        Assert.Equal(HttpStatusCode.Created, futureCreate.StatusCode);
        var futureAd = await futureCreate.Content.ReadFromJsonAsync<AdvertisementDto>();
        Assert.NotNull(futureAd);
        using var futureActive = await client.PatchAsJsonAsync($"/api/admin/advertisements/{futureAd.Id}/status", new AdvertisementStatusUpdateRequest { Status = AdvertisementStatus.Active });
        Assert.Equal(HttpStatusCode.OK, futureActive.StatusCode);

        using var filtered = await client.GetAsync("/api/mobile/advertisements/active?placement=HomeBanner");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var active = await filtered.Content.ReadFromJsonAsync<AdvertisementDto[]>();
        Assert.NotNull(active);
        Assert.Contains(active, x => x.Id == created.Id);
        Assert.DoesNotContain(active, x => x.Id == futureAd.Id);
        using var otherPlacement = await client.GetAsync("/api/mobile/advertisements/active?placement=SponsoredPoi");
        Assert.Equal(HttpStatusCode.OK, otherPlacement.StatusCode);
        var sponsored = await otherPlacement.Content.ReadFromJsonAsync<AdvertisementDto[]>();
        Assert.NotNull(sponsored);
        Assert.DoesNotContain(sponsored, x => x.Id == created.Id);

        using var home = await client.GetAsync("/api/mobile/home");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        var dashboard = await home.Content.ReadFromJsonAsync<Wayhunter.Api.Controllers.Mobile.HomeAdsAnalytics.HomeDashboardResponse>();
        Assert.NotNull(dashboard);
        Assert.Equal(admin.UserId, dashboard.Profile.UserId);
        Assert.Contains(dashboard.HomeBanners, x => x.Id == created.Id);
        Assert.DoesNotContain(dashboard.HomeBanners, x => x.Id == futureAd.Id);
        using var anonymous = factory.CreateClient();
        using var anonymousHome = await anonymous.GetAsync("/api/mobile/home");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousHome.StatusCode);

        using var persisted = new WayhunterSharedDbContext(options);
        Assert.Equal(2, await persisted.Advertisements.CountAsync(x => x.Title.StartsWith($"M7 SQL {runId} ")));
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("WAYHUNTER_M7_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("The M7 SQL connection is missing.");
        if (!TryValidateConnectionString(value))
            throw new InvalidOperationException("Only an authorized disposable LocalDB catalog is allowed.");
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(value);
        return builder.ConnectionString;
    }

    [Theory]
    [InlineData("Server=(localdb)\\WayhunterTest2026;Database=Wayhunter_Test_M7_0123456789abcdef0123456789abcdef;Integrated Security=True;TrustServerCertificate=True", true)]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=Wayhunter_Test_M7_0123456789abcdef0123456789abcdef;Integrated Security=True", true)]
    [InlineData("Server=localhost;Database=Wayhunter_Test_M7_0123456789abcdef0123456789abcdef;Integrated Security=True", false)]
    [InlineData("Server=(localdb)\\Other;Database=Wayhunter_Test_M7_0123456789abcdef0123456789abcdef;Integrated Security=True", false)]
    [InlineData("Server=(localdb)\\WayhunterTest2026;Database=Wayhunter_Test_other;Integrated Security=True", false)]
    [InlineData("Server=(localdb)\\WayhunterTest2026;Database=db82600;Integrated Security=True", false)]
    [InlineData("Server=(localdb)\\WayhunterTest2026;Database=Wayhunter_Test_M7_0123456789abcdef0123456789abcdef;User ID=test;Password=test", false)]
    public void ConnectionGuard_OnlyAllowsNamedLocalDbAndM7Catalog(string connection, bool expected)
    {
        var accepted = TryValidateConnectionString(connection);
        Assert.Equal(expected, accepted);
    }

    private static bool TryValidateConnectionString(string value)
    {
        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(value);
            var permittedInstance =
                string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(builder.DataSource, @"(localdb)\WayhunterTest2026", StringComparison.OrdinalIgnoreCase);
            return permittedInstance &&
                System.Text.RegularExpressions.Regex.IsMatch(builder.InitialCatalog, @"\AWayhunter_Test_M7_[a-f0-9]{32}\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                builder.IntegratedSecurity && builder.AttachDBFilename.Length == 0 &&
                string.IsNullOrWhiteSpace(builder.UserID) && string.IsNullOrWhiteSpace(builder.Password) &&
                !builder.InitialCatalog.Equals("db82600", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
