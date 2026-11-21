using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Services.HunterPlaces;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

public sealed partial class HunterHttpContractCandidateTests
{
    private const string Prefix = "/api/mobile/v1/hunter";
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Draft = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task DefaultApplication_DoesNotDiscoverCandidate()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Prefix + "/hunts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/v1/hunter/reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Prefix + "/drafts/" + Draft + "/submit", new { })).StatusCode);
    }

    [Fact]
    public async Task Candidate_RequiresIdentityAndReturnsOnlyOwnedResources()
    {
        using var factory = new CandidateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/hunts")).StatusCode);

        client.DefaultRequestHeaders.Add("X-Test-Subject", Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Prefix + "/hunts/" + Draft)).StatusCode);

        client.DefaultRequestHeaders.Remove("X-Test-Subject");
        client.DefaultRequestHeaders.Add("X-Test-Subject", Owner.ToString("D"));
        var draft = await client.GetFromJsonAsync<HunterSubmissionResponseDto>(Prefix + "/drafts/" + Draft);
        Assert.Equal(Draft, draft?.SubmissionId);
        Assert.Single((await client.GetFromJsonAsync<HunterSubmissionResponseDto[]>(Prefix + "/hunts"))!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Prefix + "/hunts/statistics")).StatusCode);
        var input = new HunterContractCandidateController.DraftCreate(
            "Place", null, 9, 80, 12, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync(Prefix + "/drafts", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(Prefix + "/drafts", input with { Latitude = 91 })).StatusCode);
    }

    [Fact]
    public async Task CaptureCandidate_RejectsConflictingBodySubmissionBeforeCallingHandler()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("sub", Owner.ToString("D")) }, "candidate"));
        var controller = new HunterContractCandidateController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
            }
        };
        var request = new HunterStartCaptureSessionRequestDto { HunterSubmissionId = Guid.NewGuid() };

        Assert.IsType<BadRequestResult>(await controller.StartCapture(Draft, request, null!, default));
    }

    [Fact]
    public async Task CaptureCandidate_CreatesSessionForOwnedDraftAndDeniesForeignOwner()
    {
        using var factory = new CandidateFactory();
        using var client = factory.CreateClient();
        var path = Prefix + "/drafts/" + Draft + "/capture-sessions";
        var request = new HunterStartCaptureSessionRequestDto { HunterSubmissionId = Draft };
        client.DefaultRequestHeaders.Add("X-Test-Subject", Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path, request)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Subject");
        client.DefaultRequestHeaders.Add("X-Test-Subject", Owner.ToString("D"));
        var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<HunterStartCaptureSessionResponseDto>());
    }

    [Fact]
    public async Task DiscoveryCandidates_FilterVisibilityAndDoNotExposeOwnerIdentifiers()
    {
        using var factory = new CandidateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/places")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Subject", Owner.ToString("D"));
        var places = await client.GetFromJsonAsync<HunterPublicPlaceCandidate[]>(
            Prefix + "/places?district=Jaffna&search=fort&limit=1");
        Assert.Equal(Draft, Assert.Single(places!).Id);
        var discovery = factory.Services.GetRequiredService<HunterDiscoveryCandidate>();
        Assert.Equal(Draft, Assert.Single(discovery.FindNearbyDuplicateCandidates(9, 80)).PlaceId);
        Assert.Empty(discovery.FindNearbyDuplicateCandidates(10, 80));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Prefix + "/places/" + Draft)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Prefix + "/places/" + HiddenPlace)).StatusCode);
        var ranking = await client.GetStringAsync(Prefix + "/leaderboard");
        Assert.Contains("200", ranking);
        Assert.DoesNotContain(Owner.ToString("D"), ranking);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Prefix + "/leaderboard?limit=0")).StatusCode);
    }

    [Fact]
    public async Task AdminReviewCandidates_RequireExplicitPermissionAndFilterPendingSubmissions()
    {
        using var factory = new CandidateFactory();
        using var client = factory.CreateClient();
        const string path = "/api/admin/v1/hunter/reviews";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WayhunterSharedDbContext>();
        var pending = new HunterSubmission
        {
            Status = HunterSubmissionStatus.PendingReview,
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };
        var draft = new HunterSubmission();
        db.HunterSubmissions.AddRange(pending, draft);
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Subject", Owner.ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Admin", "true");
        var rows = await client.GetFromJsonAsync<HunterSubmissionResponseDto[]>(path);
        Assert.Equal(pending.Id, Assert.Single(rows!).SubmissionId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/" + pending.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/" + draft.Id)).StatusCode);
    }

    private static readonly Guid HiddenPlace = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed class TestPublicationSource : IHunterPublishedPlaceCandidateSource
    {
        public IReadOnlyList<HunterPublicationCandidate> ReadSnapshot() =>
        [
            new(Draft, "Fort", "Jaffna", null, 9, 80, HunterSubmissionStatus.Approved, true, false),
            new(HiddenPlace, "Fort Annex", "Jaffna", null, 9, 80, HunterSubmissionStatus.Approved, true, true),
            new(Guid.NewGuid(), "Other Fort", "Jaffna", null, 9, 80, HunterSubmissionStatus.PendingReview, true, false)
        ];
    }

    private sealed class TestCaptureIdentity : IHunterCaptureSessionIdentityResolver
    {
        public Task<Guid?> ResolveSessionHunterIdAsync(string authenticatedUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(authenticatedUserId == Owner.ToString("D") ? Owner : null);
    }

    private sealed class CandidateFactory(bool verifiedLocation = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddControllers().ConfigureApplicationPartManager(manager =>
                    manager.ApplicationParts.Add(new AssemblyPart(typeof(HunterContractCandidateController).Assembly)));
                RegisterWriteCandidates(services);
                services.AddSingleton<IHunterOwnedSubmissionReader>(new TestReader());
                services.AddSingleton(new HunterDiscoveryCandidate(new TestPublicationSource()));
                services.AddSingleton<IReadOnlyList<HunterLedgerCandidate>>(
                    new[] { new HunterLedgerCandidate(Owner, "test-approval", CoinRewardType.HunterApproved, 200, false) });
                var submissionReader = new InMemoryHunterCaptureSubmissionAccessReader(
                    new[] { new HunterSubmission { Id = Draft, UserId = Owner.ToString("D") } });
                var sessions = new HunterCaptureSessionService(new InMemoryHunterCaptureSessionStore(), TimeProvider.System);
                services.AddSingleton(new HunterStartAuthorizedCaptureSessionHandler(
                    new HunterCaptureSubmissionOwnershipService(submissionReader),
                    new HunterCaptureSessionIdentityResolutionService(new TestCaptureIdentity()),
                    new HunterStartCaptureSessionHandler(sessions)));
                var now = DateTimeOffset.UtcNow;
                var account = UserAccount.RegisterPending("Hunter test", "hunter-candidate@example.test",
                    "test-only-hash", now.AddMinutes(-2));
                account.ActivateAfterVerifiedOtp(now.AddMinutes(-1));
                var dbOptions = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("D")).Options;
                services.AddScoped(_ => new WayhunterSharedDbContext(dbOptions));
                services.AddScoped<SqlHunterCaptureSubmissionAccessReader>();
                services.AddSingleton<ITrustedIdentityAccountReader>(new TestAccountReader(account));
                services.AddSingleton<IHunterLocationVerifier>(new TestLocationVerifier(verifiedLocation));
                services.AddScoped<HunterDraftCreationService>();
                services.AddScoped<HunterVerifiedDraftCandidate>();
                services.AddSingleton(new DraftAccountId(account.UserId));
                services.AddAuthorization(options => options.AddPolicy(HunterReviewContractCandidateController.Policy,
                    policy => policy.RequireAuthenticatedUser().RequireClaim("candidate-admin", "true")));
                services.AddAuthentication("candidate")
                    .AddScheme<AuthenticationSchemeOptions, CandidateAuthentication>("candidate", _ => { });
            });
        }
    }

    private sealed class CandidateAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Subject", out var subject))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new("sub", subject.ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Admin", out var admin) && admin.ToString() == "true")
                claims.Add(new Claim("candidate-admin", "true"));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    [Fact]
    public async Task DraftCandidate_CreatesOwnedDraftAfterTrustedLocationVerification()
    {
        using var factory = new CandidateFactory(verifiedLocation: true);
        using var client = factory.CreateClient();
        var owner = factory.Services.GetRequiredService<DraftAccountId>().Value;
        client.DefaultRequestHeaders.Add("X-Test-Subject", owner);

        var request = new HunterContractCandidateController.DraftCreate(
            "Fort", "Test description", 9, 80, 12, DateTimeOffset.UtcNow.AddSeconds(-1));
        var response = await client.PostAsJsonAsync(Prefix + "/drafts", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<HunterSubmissionResponseDto>();
        Assert.NotNull(created);
        Assert.Equal(HunterSubmissionStatus.Draft, created.Status);
        Assert.Equal("Jaffna", created.District);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WayhunterSharedDbContext>();
        var stored = await db.HunterSubmissions.SingleAsync();
        Assert.Equal(owner, stored.UserId);
        Assert.Equal(created.SubmissionId, stored.Id);
    }

    private sealed record DraftAccountId(string Value);

    private sealed class TestAccountReader(UserAccount account) : ITrustedIdentityAccountReader
    {
        public Task<UserAccount?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(account.UserId == userId ? account : null);
    }

    private sealed class TestLocationVerifier(bool verified) : IHunterLocationVerifier
    {
        public Task<HunterLocationVerification> VerifyAsync(
            HunterLocationSample sample, CancellationToken cancellationToken) =>
            Task.FromResult(verified
                ? new HunterLocationVerification(HunterLocationOutcome.Verified, "Jaffna")
                : new HunterLocationVerification(HunterLocationOutcome.Unavailable));
    }

    private sealed class TestReader : IHunterOwnedSubmissionReader
    {
        public Task<HunterSubmissionResponseDto?> GetMySubmissionAsync(
            string authenticatedUserId, Guid submissionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(authenticatedUserId == Owner.ToString("D") && submissionId == Draft
                ? new HunterSubmissionResponseDto { SubmissionId = Draft } : null);

        public Task<IReadOnlyList<HunterSubmissionResponseDto>> ListMySubmissionsAsync(
            string authenticatedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HunterSubmissionResponseDto>>(authenticatedUserId == Owner.ToString("D")
                ? new[] { new HunterSubmissionResponseDto { SubmissionId = Draft } }
                : Array.Empty<HunterSubmissionResponseDto>());

        public Task<int> CountMyApprovedHuntsAsync(
            string authenticatedUserId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
