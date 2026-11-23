
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
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Wayhunter.Api.Controllers.Admin.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed partial class AdvertisementAdminPositiveHttpTests
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

    private sealed partial class FakeRepository :
        IAdvertisementRepository,
        IAdvertisementWriteRepository
    {
        private readonly Dictionary<Guid, AdvertisementDto> _items = [];

        private int _getByIdCallCount;

        public int GetByIdCallCount =>
            Volatile.Read(ref _getByIdCallCount);

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AdvertisementDto> result =
                _items.Values.ToArray();

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
            DateTimeOffset nowUtc,
            AdvertisementPlacement? placement = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AdvertisementDto> result = _items.Values
                .Where(ad =>
                    ad.IsEnabled &&
                    ad.Status == AdvertisementStatus.Active &&
                    ad.StartAtUtc <= nowUtc &&
                    nowUtc < ad.EndAtUtc &&
                    (!placement.HasValue || ad.Placement == placement.Value))
                .ToArray();
            return Task.FromResult(result);
        }

        public void AddForMobileRead(AdvertisementDto advertisement)
        {
            _items.Add(advertisement.Id, advertisement);
        }

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getByIdCallCount);
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

    private static async Task<WebApplication> CreateServerAsync(bool enableImageUpload = false, bool useKestrel = false, bool enableLifecycle = false)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
        // Windows Event Log is not writable in the test runner and can mask
        // the HTTP exception under test with a logging-provider failure.
        builder.Logging.ClearProviders();

        if (useKestrel)
        {
            // Test-only HTTP listener on the local loopback interface.
            // Port zero requests an available ephemeral port.
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        }
        else
        {
            builder.WebHost.UseTestServer();
        }

        builder.Configuration["Advertisements:ImageUpload:Enabled"] = enableImageUpload.ToString();
        builder.Configuration["Advertisements:ImageUpload:LifecycleEnabled"] = enableLifecycle.ToString();

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

        builder.Services.AddScoped<AdvertisementService>();

        builder.Services.AddScoped<IAdvertisementWriteRepository>(
            sp => sp.GetRequiredService<FakeRepository>());

        if (enableLifecycle)
        {
            // Test-only fakes. Never contact SQL or Cloudinary.
            builder.Services.AddSingleton<FakeImageStorage>();

            builder.Services.AddScoped<IAdvertisementImageStorage>(
                sp => sp.GetRequiredService<FakeImageStorage>());

            builder.Services.AddScoped<IAdvertisementImageUpdateRepository>(
                sp => sp.GetRequiredService<FakeRepository>());

            builder.Services.AddScoped<
                Wayhunter.Api.Services.HomeAdsAnalytics.AdvertisementImageLifecycleService>();
        }

        builder.Services.AddSingleton(TimeProvider.System);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();

        return app;
    }

    [Fact]
    public async Task MobileActiveListing_AppliesPlacementAndServerSchedule()
    {
        await using var app = await CreateServerAsync();
        var now = DateTimeOffset.UtcNow;
        var repository = app.Services.GetRequiredService<FakeRepository>();

        var homeAd = new AdvertisementDto
        {
            Id = Guid.NewGuid(),
            Title = "Current home ad",
            ImageUrl = "https://example.com/home.jpg",
            StartAtUtc = now.AddMinutes(-5),
            EndAtUtc = now.AddMinutes(5),
            IsEnabled = true,
            Placement = AdvertisementPlacement.HomeBanner,
            Status = AdvertisementStatus.Active
        };
        var poiAd = homeAd with
        {
            Id = Guid.NewGuid(),
            Placement = AdvertisementPlacement.SponsoredPoi
        };
        var futureAd = homeAd with
        {
            Id = Guid.NewGuid(),
            StartAtUtc = now.AddMinutes(5),
            EndAtUtc = now.AddMinutes(10)
        };
        repository.AddForMobileRead(homeAd);
        repository.AddForMobileRead(poiAd);
        repository.AddForMobileRead(futureAd);

        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "traveler");
        using var response = await client.GetAsync(
            "/" + AdvertisementMobileReadEndpointContract.ActiveAdsRoute +
            "?placement=HomeBanner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var advertisements = await response.Content
            .ReadFromJsonAsync<AdvertisementDto[]>();
        var onlyAdvertisement = Assert.Single(advertisements!);
        Assert.Equal(homeAd.Id, onlyAdvertisement.Id);
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


    // ==========================================
    // M7 Secure Image Upload - Guard Tests
    // ==========================================

    [Fact]
    public async Task Traveler_AdvertisementImageUpload_Returns403()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "traveler");

        using var form = new MultipartFormDataContent();

        using var response = await client.PostAsync(
            $"{Route}/{Guid.NewGuid()}/image",
            form);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Admin_AdvertisementImageUpload_Disabled_Returns503()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        using var form = new MultipartFormDataContent();

        using var file = new ByteArrayContent(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "test.png");

        using var response = await client.PostAsync(
            $"{Route}/{Guid.NewGuid()}/image",
            form);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected 503, got {(int)response.StatusCode}. Body: {responseBody}");
    }

    [Fact]
    public async Task Admin_ImageUpload_MissingAdvertisement_Returns404()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true);

        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "test.png");

        using var response = await client.PostAsync(
            $"{Route}/{Guid.NewGuid()}/image",
            form);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Admin_ImageUpload_ExistingAdvertisement_RemainsDisabled()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");

        using var form = new MultipartFormDataContent();
        using var bitmap = new SkiaSharp.SKBitmap(16, 16);
        bitmap.Erase(SkiaSharp.SKColors.Blue);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(
            SkiaSharp.SKEncodedImageFormat.Png, 100);

        Assert.NotNull(encoded);
        using var file = new ByteArrayContent(
            encoded!.ToArray());

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "test.png");

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image",
            form);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        var unchanged = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.NotNull(unchanged);
        Assert.Equal(
            advertisement.ImageUrl,
            unchanged!.ImageUrl);
    }

    [Fact]
    public async Task Admin_ImageUpload_FeatureOff_DoesNotAccessRepository()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: false);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var readsBefore = repository.GetByIdCallCount;

        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        using var form = new MultipartFormDataContent();

        using var file = new ByteArrayContent(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "test.png");

        using var response = await client.PostAsync(
            $"{Route}/{Guid.NewGuid()}/image",
            form);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        Assert.Equal(
            readsBefore,
            repository.GetByIdCallCount);
    }

    // ==========================================
    // M7 Image Upload - HTTP Security Rejections
    // ==========================================

    [Fact]
    public async Task Admin_ImageUpload_InvalidPng_Returns400()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");

        using var form = new MultipartFormDataContent();

        using var file = new ByteArrayContent(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "fake.png");

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchanged = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.Equal(advertisement.ImageUrl, unchanged!.ImageUrl);
    }

    [Fact]
    public async Task Admin_ImageUpload_TwoFiles_Returns400()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");

        using var form = new MultipartFormDataContent();

        using var first = new ByteArrayContent(new byte[] { 1, 2 });
        using var second = new ByteArrayContent(new byte[] { 3, 4 });

        first.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        second.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        form.Add(first, "file", "first.png");
        form.Add(second, "file", "second.png");

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchanged = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.Equal(advertisement.ImageUrl, unchanged!.ImageUrl);
    }

    [Fact]
    public async Task Admin_ImageUpload_ExtraField_Returns400()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        using var bitmap = new SkiaSharp.SKBitmap(16, 16);
        bitmap.Erase(SkiaSharp.SKColors.Blue);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(
            SkiaSharp.SKEncodedImageFormat.Png, 100);

        Assert.NotNull(encoded);

        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");

        using var form = new MultipartFormDataContent();

        using var file = new ByteArrayContent(
            encoded!.ToArray());

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "valid.png");
        form.Add(new StringContent("unexpected"), "extra");

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchanged = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.Equal(advertisement.ImageUrl, unchanged!.ImageUrl);
    }

    [Fact]
    public async Task Admin_ImageUpload_OversizedFile_IsRejected()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        using var form = new MultipartFormDataContent();

        // Exactly one byte beyond the 5 MB file limit.
        using var file = new ByteArrayContent(
            new byte[5 * 1024 * 1024 + 1]);

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "oversized.png");

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image",
            form);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.RequestEntityTooLarge,
            $"Unexpected HTTP status: {(int)response.StatusCode}");

        var unchanged = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.NotNull(unchanged);
        Assert.Equal(
            advertisement.ImageUrl,
            unchanged!.ImageUrl);
    }

    [Fact]
    public async Task Admin_ImageUpload_MalformedBoundary_Returns400()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        using var content = new ByteArrayContent(
            System.Text.Encoding.UTF8.GetBytes(
                "--incorrect-boundary\r\n" +
                "Content-Disposition: form-data; name=\"file\"; " +
                "filename=\"image.png\"\r\n" +
                "Content-Type: image/png\r\n\r\n" +
                "invalid-data\r\n" +
                "--incorrect-boundary--\r\n"));

        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "multipart/form-data");

        content.Headers.ContentType.Parameters.Add(
            new System.Net.Http.Headers.NameValueHeaderValue(
                "boundary", "expected-boundary"));

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var unchanged = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.NotNull(unchanged);
        Assert.Equal(
            advertisement.ImageUrl,
            unchanged!.ImageUrl);
    }

    [Fact]
    public async Task Admin_ImageUpload_KestrelOversizedRequest_IsRejected()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true,
            useKestrel: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        var address = server.Urls.Single();

        using var client = new HttpClient
        {
            BaseAddress = new Uri(address),
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.Add(
            "X-Test-User", "admin");

        // First confirm the test-only Admin request
        // reaches the intended controller.
        using var bitmap = new SkiaSharp.SKBitmap(16, 16);
        bitmap.Erase(SkiaSharp.SKColors.Blue);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(
            SkiaSharp.SKEncodedImageFormat.Png, 100);

        Assert.NotNull(encoded);

        using (var validForm = new MultipartFormDataContent())
        {
            using var validFile = new ByteArrayContent(
                encoded!.ToArray());

            validFile.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "image/png");

            validForm.Add(validFile, "file", "valid.png");

            using var validResponse = await client.PostAsync(
                $"{Route}/{advertisement.Id}/image",
                validForm);

            Assert.Equal(
                HttpStatusCode.ServiceUnavailable,
                validResponse.StatusCode);
        }

        // Exceeds the endpoint's total HTTP request limit,
        // not merely its 5 MB file validation limit.
        using var oversizedForm = new MultipartFormDataContent();

        using var oversizedFile = new ByteArrayContent(
            new byte[5 * 1024 * 1024 + 131072]);

        oversizedFile.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        oversizedForm.Add(
            oversizedFile, "file", "oversized.png");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{Route}/{advertisement.Id}/image")
        {
            Content = oversizedForm
        };

        request.Headers.ExpectContinue = true;

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);

        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine(
            $"Kestrel oversized status: {(int)response.StatusCode}");

        Console.WriteLine(
            $"Kestrel oversized response: {responseBody}");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Contains(
            "Request body too large",
            responseBody,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "5308416",
            responseBody,
            StringComparison.Ordinal);

        var unchanged = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.NotNull(unchanged);
        Assert.Equal(
            advertisement.ImageUrl,
            unchanged!.ImageUrl);
    }
}
