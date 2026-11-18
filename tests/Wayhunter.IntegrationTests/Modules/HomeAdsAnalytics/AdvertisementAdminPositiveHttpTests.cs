
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Wayhunter.Api.Authorization;
using Wayhunter.Api.Controllers.Admin.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementAdminPositiveHttpTests
{
    private const string TestSchemeName = "M7AdminHttpTest";
    private const string Route = "/api/admin/advertisements";

    // ==========================================
    // Test-only Authentication
    // ==========================================

    private sealed class TestAuthHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult>
            HandleAuthenticateAsync()
        {
            var userType =
                Request.Headers["X-Test-User"].ToString();

            if (userType != "admin" &&
                userType != "traveler")
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    "11111111-1111-4111-8111-111111111111")
            };

            if (userType == "admin")
            {
                claims.Add(
                    new Claim(ClaimTypes.Role, "Admin"));
            }

            // Test-only role claim.
            // Real production roles must come from M1.
            var identity = new ClaimsIdentity(
                claims,
                TestSchemeName);

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(
                        new ClaimsPrincipal(identity),
                        TestSchemeName)));
        }
    }

    // ==========================================
    // Fake Advertisement Repository - No SQL
    // ==========================================

    private sealed class FakeRepository :
        IAdvertisementRepository,
        IAdvertisementWriteRepository
    {
        private readonly Dictionary<Guid, AdvertisementDto> _items = [];

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AdvertisementDto> result =
                _items.Values.ToArray();

            return Task.FromResult(result);
        }

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(id, out var item);
            return Task.FromResult(item);
        }

        public Task<AdvertisementDto> CreateAsync(
            AdvertisementCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            var advertisement = new AdvertisementDto
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                ImageUrl = request.ImageUrl,
                DestinationUrl = request.DestinationUrl,
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                Placement = request.Placement,
                Status = AdvertisementStatus.Draft,
                IsEnabled = false
            };

            _items.Add(
                advertisement.Id,
                advertisement);

            return Task.FromResult(advertisement);
        }

        public Task<AdvertisementDto?> UpdateAsync(
            Guid id,
            AdvertisementUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_items.TryGetValue(id, out var advertisement))
            {
                return Task.FromResult<AdvertisementDto?>(null);
            }

            if (advertisement.Status == AdvertisementStatus.Archived)
            {
                throw new InvalidOperationException(
                    "Archived advertisements cannot be edited.");
            }

            advertisement = advertisement with
            {
                Title = request.Title,
                ImageUrl = request.ImageUrl,
                DestinationUrl = request.DestinationUrl,
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                Placement = request.Placement
            };

            _items[id] = advertisement;

            return Task.FromResult<AdvertisementDto?>(
                advertisement);
        }

        public Task<AdvertisementDto?> ChangeStatusAsync(
            Guid id,
            AdvertisementStatusUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_items.TryGetValue(id, out var advertisement))
            {
                return Task.FromResult<AdvertisementDto?>(null);
            }

            var status = request.Status!.Value;

            if (status == AdvertisementStatus.Active &&
                advertisement.Status == AdvertisementStatus.Archived)
            {
                throw new InvalidOperationException(
                    "Archived advertisements cannot be activated.");
            }

            if (status == AdvertisementStatus.Paused &&
                advertisement.Status == AdvertisementStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Draft advertisements cannot be paused.");
            }

            advertisement = advertisement with
            {
                Status = status,
                IsEnabled = status == AdvertisementStatus.Active
            };

            _items[id] = advertisement;

            return Task.FromResult<AdvertisementDto?>(
                advertisement);
        }
    }

    // ==========================================
    // Test Server Setup
    // ==========================================

    private static async Task<WebApplication> CreateServerAsync()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });

        builder.WebHost.UseTestServer();

        builder.Configuration[
            "Identity:AdministratorRole"] = "Admin";

        builder.Services
            .AddControllers()
            .AddApplicationPart(
                typeof(AdvertisementsController).Assembly);

        builder.Services
            .AddAuthentication(TestSchemeName)
            .AddScheme<
                AuthenticationSchemeOptions,
                TestAuthHandler>(
                TestSchemeName,
                _ => { });

        builder.Services.AddAdvertisementAdministratorPolicy(
            builder.Configuration);

        builder.Services.AddSingleton<FakeRepository>();

        builder.Services.AddScoped<IAdvertisementRepository>(
            sp => sp.GetRequiredService<FakeRepository>());

        builder.Services.AddScoped<IAdvertisementWriteRepository>(
            sp => sp.GetRequiredService<FakeRepository>());

        builder.Services.AddSingleton(TimeProvider.System);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();

        return app;
    }

    // ==========================================
    // Valid Create Request
    // ==========================================

    private static AdvertisementCreateRequest ValidCreate() => new()
    {
        Title = "M7 HTTP Advertisement",
        ImageUrl = "https://example.com/banner.jpg",
        DestinationUrl = "https://example.com/travel",
        StartAtUtc = new DateTimeOffset(
            2026, 11, 18, 0, 0, 0, TimeSpan.Zero),
        EndAtUtc = new DateTimeOffset(
            2026, 12, 18, 0, 0, 0, TimeSpan.Zero),
        Placement = AdvertisementPlacement.HomeBanner
    };

    // ==========================================
    // TEST 01 - Admin Create / Read / Update / Status
    // ==========================================

    [Fact]
    public async Task Admin_CreateReadUpdateStatus_ReturnsSuccess()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "admin");

        // CREATE
        using var createResponse =
            await client.PostAsJsonAsync(
                Route,
                ValidCreate());

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<AdvertisementDto>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(
            AdvertisementStatus.Draft,
            created.Status);

        // READ
        using var readResponse =
            await client.GetAsync($"{Route}/{created.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            readResponse.StatusCode);

        // UPDATE
        var original = ValidCreate();

        var updateRequest = new AdvertisementUpdateRequest
        {
            Title = "Updated M7 Advertisement",
            ImageUrl = original.ImageUrl,
            DestinationUrl = original.DestinationUrl,
            StartAtUtc = original.StartAtUtc,
            EndAtUtc = original.EndAtUtc,
            Placement = AdvertisementPlacement.SponsoredPoi
        };

        using var updateResponse =
            await client.PutAsJsonAsync(
                $"{Route}/{created.Id}",
                updateRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        // ACTIVE
        using var activeResponse =
            await client.PatchAsJsonAsync(
                $"{Route}/{created.Id}/status",
                new AdvertisementStatusUpdateRequest
                {
                    Status = AdvertisementStatus.Active
                });

        Assert.Equal(
            HttpStatusCode.OK,
            activeResponse.StatusCode);

        // PAUSED
        using var pausedResponse =
            await client.PatchAsJsonAsync(
                $"{Route}/{created.Id}/status",
                new AdvertisementStatusUpdateRequest
                {
                    Status = AdvertisementStatus.Paused
                });

        Assert.Equal(
            HttpStatusCode.OK,
            pausedResponse.StatusCode);

        // ARCHIVED
        using var archivedResponse =
            await client.PatchAsJsonAsync(
                $"{Route}/{created.Id}/status",
                new AdvertisementStatusUpdateRequest
                {
                    Status = AdvertisementStatus.Archived
                });

        Assert.Equal(
            HttpStatusCode.OK,
            archivedResponse.StatusCode);

        var archived =
            await archivedResponse.Content
                .ReadFromJsonAsync<AdvertisementDto>();

        Assert.NotNull(archived);
        Assert.Equal(
            AdvertisementStatus.Archived,
            archived.Status);
        Assert.False(archived.IsEnabled);

        // Archived advertisement cannot be activated.
        using var conflictResponse =
            await client.PatchAsJsonAsync(
                $"{Route}/{created.Id}/status",
                new AdvertisementStatusUpdateRequest
                {
                    Status = AdvertisementStatus.Active
                });

        Assert.Equal(
            HttpStatusCode.Conflict,
            conflictResponse.StatusCode);
    }

    // ==========================================
    // TEST 02 - Invalid Create Returns 400
    // ==========================================

    [Fact]
    public async Task Admin_InvalidCreate_Returns400()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "admin");

        using var response = await client.PostAsJsonAsync(
            Route,
            ValidCreate() with
            {
                Title = " "
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // ==========================================
    // TEST 03 - Missing Advertisement Returns 404
    // ==========================================

    [Fact]
    public async Task Admin_MissingAdvertisement_Returns404()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "admin");

        using var response =
            await client.GetAsync(
                $"{Route}/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // ==========================================
    // TEST 04 - Anonymous Returns 401
    // ==========================================

    [Fact]
    public async Task Anonymous_AdminGet_Returns401()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        using var response = await client.GetAsync(Route);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ==========================================
    // TEST 05 - Traveler Returns 403
    // ==========================================

    [Fact]
    public async Task Traveler_AdminGet_Returns403()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "traveler");

        using var response = await client.GetAsync(Route);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
   
    }

    // ==========================================
    // TEST 06 - Invalid Update Returns 400
    // ==========================================

    [Fact]
    public async Task Admin_InvalidUpdate_Returns400()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        var original = ValidCreate();

        var invalidUpdate = new AdvertisementUpdateRequest
        {
            Title = " ",
            ImageUrl = original.ImageUrl,
            DestinationUrl = original.DestinationUrl,
            StartAtUtc = original.StartAtUtc,
            EndAtUtc = original.EndAtUtc,
            Placement = original.Placement
        };

        using var response = await client.PutAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}",
            invalidUpdate);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // ==========================================
    // TEST 07 - Invalid Status Returns 400
    // ==========================================

    [Fact]
    public async Task Admin_InvalidStatus_Returns400()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        using var response = await client.PatchAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}/status",
            new AdvertisementStatusUpdateRequest
            {
                Status = null
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // ==========================================
    // TEST 08 - Missing Update Returns 404
    // ==========================================

    [Fact]
    public async Task Admin_MissingUpdate_Returns404()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        var original = ValidCreate();

        var update = new AdvertisementUpdateRequest
        {
            Title = "Updated advertisement",
            ImageUrl = original.ImageUrl,
            DestinationUrl = original.DestinationUrl,
            StartAtUtc = original.StartAtUtc,
            EndAtUtc = original.EndAtUtc,
            Placement = original.Placement
        };

        using var response = await client.PutAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}",
            update);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // ==========================================
    // TEST 09 - Missing Status Returns 404
    // ==========================================

    [Fact]
    public async Task Admin_MissingStatus_Returns404()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        using var response = await client.PatchAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}/status",
            new AdvertisementStatusUpdateRequest
            {
                Status = AdvertisementStatus.Active
            });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // ==========================================
    // TEST 10 - Archived Update Returns 409
    // ==========================================

    [Fact]
    public async Task Admin_ArchivedUpdate_Returns409()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        using var createResponse =
            await client.PostAsJsonAsync(
                Route,
                ValidCreate());

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<AdvertisementDto>();

        Assert.NotNull(created);

        using var archiveResponse =
            await client.PatchAsJsonAsync(
                $"{Route}/{created.Id}/status",
                new AdvertisementStatusUpdateRequest
                {
                    Status = AdvertisementStatus.Archived
                });

        Assert.Equal(
            HttpStatusCode.OK,
            archiveResponse.StatusCode);

        var original = ValidCreate();

        var update = new AdvertisementUpdateRequest
        {
            Title = "Attempted archived update",
            ImageUrl = original.ImageUrl,
            DestinationUrl = original.DestinationUrl,
            StartAtUtc = original.StartAtUtc,
            EndAtUtc = original.EndAtUtc,
            Placement = original.Placement
        };

        using var response = await client.PutAsJsonAsync(
            $"{Route}/{created.Id}",
            update);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

}
