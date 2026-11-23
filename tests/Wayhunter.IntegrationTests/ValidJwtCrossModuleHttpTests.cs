using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Queries;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests;

public sealed class ValidJwtCrossModuleHttpTests
{
    private const string SigningSecret =
        "test-only-cross-module-secret-1234567890";

    private sealed class TestSessionReader
        : IAuthenticatedSessionReader
    {
        private readonly UserAccount[] _accounts;
        private readonly IdentitySession[] _sessions;

        public TestSessionReader(
            UserAccount[] accounts,
            IdentitySession[] sessions)
        {
            _accounts = accounts;
            _sessions = sessions;
        }

        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _accounts.SingleOrDefault(
                    a => a.UserId == userId));
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _sessions.SingleOrDefault(
                    s => s.SessionId == sessionId));
        }
    }

    private sealed class TestWalletReader : ICoinWalletReader
    {
        private readonly string _ownerId;
        private readonly string _otherId;

        public bool ReturnMissingWallet { get; set; }

        public TestWalletReader(
            string ownerId,
            string otherId)
        {
            _ownerId = ownerId;
            _otherId = otherId;
        }

        public Task<CoinWalletSnapshot?> GetByOwnerUserIdAsync(
            string ownerUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ReturnMissingWallet)
            {
                return Task.FromResult<CoinWalletSnapshot?>(null);
            }

            CoinWalletSnapshot? snapshot = null;

            if (ownerUserId == _ownerId)
            {
                snapshot = new CoinWalletSnapshot(
                    _ownerId,
                    new CoinWalletBalance(200, 0, 200));
            }
            else if (ownerUserId == _otherId)
            {
                snapshot = new CoinWalletSnapshot(
                    _otherId,
                    new CoinWalletBalance(50, 0, 50));
            }

            return Task.FromResult(snapshot);
        }
    }

    private sealed class AuthenticatedFactory
        : WebApplicationFactory<Program>
    {
        public UserAccount Owner { get; }
        public UserAccount Other { get; }

        public IdentitySession OwnerSession { get; }
        public IdentitySession OtherSession { get; }

        public TestWalletReader WalletReader { get; private set; } = null!;

        public AuthenticatedFactory()
        {
            var now = DateTimeOffset.UtcNow;

            Owner = CreateAccount(
                "Owner",
                "owner@example.com",
                now);

            Other = CreateAccount(
                "Other",
                "other@example.com",
                now);

            OwnerSession = IdentitySession.Create(
                Owner.UserId,
                now.AddMinutes(-10),
                now.AddHours(1));

            OtherSession = IdentitySession.Create(
                Other.UserId,
                now.AddMinutes(-10),
                now.AddHours(1));
        }

        private static UserAccount CreateAccount(
            string name,
            string email,
            DateTimeOffset now)
        {
            var account = UserAccount.RegisterPending(
                name,
                email,
                "test-password-hash",
                now.AddHours(-2));

            account.ActivateAfterVerifiedOtp(
                now.AddHours(-1));

            return account;
        }

        protected override IHost CreateHost(
            IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Identity:EnableSharedAuthentication"] =
                            "true",

                        ["ConnectionStrings:WayhunterSharedDb"] =
                            "Server=localhost;" +
                            "Database=WayhunterJwtTest;" +
                            "Trusted_Connection=True;" +
                            "TrustServerCertificate=True",

                        ["Authentication:Jwt:Issuer"] =
                            "cross-module-test",

                        ["Authentication:Jwt:Audience"] =
                            "wayhunter-mobile-test",

                        ["Authentication:Jwt:SigningSecret"] =
                            SigningSecret
                    });
            });

            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticatedSessionReader>();

                services.AddSingleton<IAuthenticatedSessionReader>(
                    new TestSessionReader(
                        new[] { Owner, Other },
                        new[] { OwnerSession, OtherSession }));

                WalletReader = new TestWalletReader(
                    Owner.UserId,
                    Other.UserId);

                services.RemoveAll<ICoinWalletReader>();

                services.AddSingleton<ICoinWalletReader>(
                    WalletReader);

                services.AddScoped<GetMyCoinWalletService>();
            });
        }

        public HttpClient CreateAuthenticatedClient(
            UserAccount account,
            IdentitySession session)
        {
            var client = CreateClient();

            var tokenService = Services.GetRequiredService<
                SignedSessionTokenService>();

            var token = tokenService.Issue(
                account,
                session,
                DateTimeOffset.UtcNow);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);

            return client;
        }

        public HttpClient OwnerClient()
            => CreateAuthenticatedClient(
                Owner,
                OwnerSession);

        public HttpClient OtherClient()
            => CreateAuthenticatedClient(
                Other,
                OtherSession);
    }

    [Fact]
    public async Task M2_OnlyTripOwner_CanReadTrip()
    {
        using var factory = new AuthenticatedFactory();
        using var owner = factory.OwnerClient();
        using var other = factory.OtherClient();

        var startDate = DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(2));

        var request = new CreateTripPlanDto(
            "JWT Integration Test Trip",
            startDate,
            startDate.AddDays(1),
            5000m,
            "Car",
            2);

        using var created = await owner.PostAsJsonAsync(
            "/api/mobile/trip-plans",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            created.StatusCode);

        var trip = await created.Content
            .ReadFromJsonAsync<TripPlanResponseDto>();

        Assert.NotNull(trip);

        var path =
            $"/api/mobile/trip-plans/{trip.TripPlanId}";

        using var ownerResponse =
            await owner.GetAsync(path);

        using var otherResponse =
            await other.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            otherResponse.StatusCode);
    }

    private sealed class FinishSaveInterceptor : SaveChangesInterceptor
    {
        public bool FailSave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailSave)
                throw new DbUpdateException("Simulated Finish persistence failure.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task M3_Finish_CleansUpOnlyAfterPersistence_AndIsRepeatable()
    {
        var interceptor = new FinishSaveInterceptor();
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        using var authFactory = new AuthenticatedFactory();
        using var factory = authFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILiveTripSessionRepository>();
                services.AddScoped<ILiveTripSessionRepository>(provider =>
                    new EfLiveTripSessionRepository(
                        provider.GetRequiredKeyedService<WayhunterSharedDbContext>("finish-test")));
                services.AddKeyedScoped<WayhunterSharedDbContext>("finish-test",
                    (_, _) => new WayhunterSharedDbContext(options));
            }));

        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        var tokenService = factory.Services.GetRequiredService<SignedSessionTokenService>();
        var now = DateTimeOffset.UtcNow;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokenService.Issue(authFactory.Owner, authFactory.OwnerSession, now));
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokenService.Issue(authFactory.Other, authFactory.OtherSession, now));

        var startDate = DateOnly.FromDateTime(now.UtcDateTime);
        var trip = new TripPlan(authFactory.Owner.UserId, "Finish integration trip",
            startDate, startDate.AddDays(1), 5000m, "Car", 2);
        var members = factory.Services.GetRequiredService<InMemoryTripMembershipRevocationRepository>();
        await members.AddTripAsync(trip);

        await using var seed = new WayhunterSharedDbContext(options);
        var session = LiveTripSession.Start(Guid.NewGuid(), trip.Id, now.AddMinutes(-10));
        await new EfLiveTripSessionRepository(seed).AddAsync(session);

        var registry = factory.Services.GetRequiredService<TripLocationSubscriberRegistry>();
        registry.Register("target", trip.Id, session.SessionId, authFactory.Owner.UserId);
        registry.Register("other-session", trip.Id, Guid.NewGuid(), authFactory.Owner.UserId);
        var route = $"/api/mobile/trips/{trip.Id}/sessions/{session.SessionId}/finish";

        using var denied = await other.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.NotNull(registry.Find("target"));

        interceptor.FailSave = true;
        using var failed = await owner.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.NotNull(registry.Find("target"));

        await using var read = new WayhunterSharedDbContext(options);
        Assert.Equal(LiveTripStatus.Active,
            await read.LiveTripSessions.Where(x => x.SessionId == session.SessionId)
                .Select(x => x.Status).SingleAsync());

        interceptor.FailSave = false;
        using var finished = await owner.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);
        var first = await finished.Content.ReadFromJsonAsync<TripSessionDto>();
        Assert.NotNull(first);
        Assert.Equal(LiveTripStatus.Finished, first.Status);
        Assert.NotNull(first.FinishedAtUtc);
        var saved = await read.LiveTripSessions.AsNoTracking().SingleAsync(x => x.SessionId == session.SessionId);
        Assert.Equal(LiveTripStatus.Finished, saved.Status);
        Assert.Equal(first.FinishedAtUtc, saved.FinishedAtUtc);
        Assert.Null(registry.Find("target"));
        Assert.NotNull(registry.Find("other-session"));

        // Repeating Finish should return the saved result without attempting another write.
        interceptor.FailSave = true;
        using var repeated = await owner.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(first, await repeated.Content.ReadFromJsonAsync<TripSessionDto>());
        Assert.NotNull(registry.Find("other-session"));
    }

    [Fact]
    public async Task M3_OnlyAcceptedMember_CanGrantGpsConsent()
    {
        using var factory = new AuthenticatedFactory();
        using var owner = factory.OwnerClient();
        using var other = factory.OtherClient();

        var repository =
            Assert.IsType<InMemoryTripSharingConsentRepository>(
                factory.Services.GetRequiredService<
                    ITripSharingConsentRepository>());

        var tripId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        repository.SeedSessionForTests(
            LiveTripSession.Start(
                liveSessionId,
                tripId,
                now.AddMinutes(-6)));

        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            tripId,
            factory.Other.UserId,
            factory.Owner.UserId,
            now.AddMinutes(-9));

        invitation.Accept(
            now.AddMinutes(-8));

        var member =
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation);

        repository.SeedAcceptedMemberForTests(
            member);

        var path =
            $"/api/mobile/trips/{tripId}/sessions/" +
            $"{liveSessionId}/gps-consent";

        using var accepted =
            await owner.PutAsJsonAsync(
                path,
                new { isGranted = true });

        Assert.Equal(
            HttpStatusCode.OK,
            accepted.StatusCode);

        var consent = await accepted.Content
            .ReadFromJsonAsync<GpsConsentDto>();

        Assert.NotNull(consent);
        Assert.True(consent.IsGranted);

        Assert.Equal(
            factory.Owner.UserId,
            consent.UserId);

        using var rejected =
            await other.PutAsJsonAsync(
                path,
                new { isGranted = true });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            rejected.StatusCode);

        Assert.Null(
            repository.GetSnapshotForTests(
                liveSessionId,
                factory.Other.UserId));
    }

    [Fact]
    public async Task M6_Wallet_IsScopedToAuthenticatedUser()
    {
        using var factory = new AuthenticatedFactory();
        using var owner = factory.OwnerClient();
        using var other = factory.OtherClient();

        // Supplying another user's ID in a query string
        // must not change the authenticated wallet owner.
        using var ownerResponse = await owner.GetAsync(
            "/api/mobile/coins-wallet/me?userId=" +
            factory.Other.UserId);

        using var otherResponse = await other.GetAsync(
            "/api/mobile/coins-wallet/me?userId=" +
            factory.Owner.UserId);

        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            otherResponse.StatusCode);

        var ownerWallet =
            await ownerResponse.Content
                .ReadFromJsonAsync<MyCoinWalletResponse>();

        var otherWallet =
            await otherResponse.Content
                .ReadFromJsonAsync<MyCoinWalletResponse>();

        Assert.NotNull(ownerWallet);
        Assert.NotNull(otherWallet);

        Assert.Equal(
            200L,
            ownerWallet.PostedCoins);

        Assert.Equal(
            50L,
            otherWallet.PostedCoins);
    }

    [Fact]
    public async Task M6_Wallet_UnauthenticatedRequest_Returns401()
    {
        using var factory = new AuthenticatedFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/mobile/coins-wallet/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task M6_Wallet_MissingWallet_Returns404()
    {
        using var factory = new AuthenticatedFactory();
        using var client = factory.OwnerClient();

        factory.WalletReader.ReturnMissingWallet = true;

        using var response = await client.GetAsync(
            "/api/mobile/coins-wallet/me");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task RevokedSession_ValidJwt_Returns401()
    {
        using var factory = new AuthenticatedFactory();
        using var client = factory.OwnerClient();

        factory.OwnerSession.Revoke(
            DateTimeOffset.UtcNow);

        using var response = await client.GetAsync(
            "/api/mobile/coins-wallet/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task SuspendedAccount_ValidJwt_Returns401()
    {
        using var factory = new AuthenticatedFactory();
        using var client = factory.OwnerClient();

        factory.Owner.Suspend();

        using var response = await client.GetAsync(
            "/api/mobile/coins-wallet/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
