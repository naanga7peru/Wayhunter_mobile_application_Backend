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
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.CoinsWallet;

public sealed class M6SqlVoucherHttpFactAttribute : FactAttribute
{
    public M6SqlVoucherHttpFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M6_TEST_SQL_CONNECTION")))
            Skip = "Set the isolated M6 SQL connection to run voucher HTTP-to-SQL acceptance.";
    }
}

public sealed class M6SqlVoucherHttpE2ETests
{
    private const string SigningSecret = "test-only-32-byte-or-longer-jwt-signing-key-123";
    private const string Issuer = "wayhunter-m6-test";
    private const string Audience = "wayhunter-mobile-test";
    private const string ProtectorKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    [M6SqlVoucherHttpFact]
    public async Task AuthenticatedVoucherRequest_ReservesApprovesAndIssuesExactlyOnce()
    {
        var connection = GetConnectionString();
        var now = DateTimeOffset.UtcNow;
        var owner = CreateVerifiedUser("M6 voucher owner", now);
        var admin = CreateVerifiedUser("M6 voucher admin", now);
        var stranger = CreateVerifiedUser("M6 voucher stranger", now);
        var ownerSession = IdentitySession.Create(owner.UserId, now.AddMinutes(-1), now.AddHours(1));
        var adminSession = IdentitySession.Create(admin.UserId, now.AddMinutes(-1), now.AddHours(1));
        var strangerSession = IdentitySession.Create(stranger.UserId, now.AddMinutes(-1), now.AddHours(1));
        var walletId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseSqlServer(connection).Options;
        await using (var db = new WayhunterSharedDbContext(options))
        {
            db.Users.AddRange(owner, admin, stranger);
            db.IdentitySessions.AddRange(ownerSession, adminSession, strangerSession);
            db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(admin.UserId, now, null));
            db.CoinWallets.Add(new CoinWalletRow
            {
                Id = walletId,
                UserId = owner.UserId,
                PostedCoins = 10_100,
                ReservedCoins = 0,
                LifetimeEarnedCoins = 10_100
            });
            db.CoinLedgerEntries.Add(new CoinLedgerEntryRow
            {
                Id = Guid.NewGuid(), WalletId = walletId, DeltaCoins = 10_100,
                SourceRecordId = $"m6-test-seed:{Guid.NewGuid():N}", RuleVersion = "m6-test-seed-v1",
                CorrelationId = Guid.NewGuid(), RecordedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        var reward = new CoinRewardGrantRequest(
            Guid.NewGuid(), CoinRewardType.WelcomeVerified, owner.UserId, owner.UserId,
            100, now, Guid.NewGuid(), "m6-e2e-v1");
        await using (var firstGrantContext = new WayhunterSharedDbContext(options))
            Assert.True(await new Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet.SqlCoinRewardGrantStore(firstGrantContext).TryGrantOnceAsync(reward));
        await using (var duplicateGrantContext = new WayhunterSharedDbContext(options))
            Assert.False(await new Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet.SqlCoinRewardGrantStore(duplicateGrantContext).TryGrantOnceAsync(reward));

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:WayhunterSharedDb", connection);
            builder.UseSetting("Identity:EnableSharedAuthentication", "true");
            builder.UseSetting("Identity:AdministratorRole", "Admin");
            builder.UseSetting("Authentication:Jwt:Issuer", Issuer);
            builder.UseSetting("Authentication:Jwt:Audience", Audience);
            builder.UseSetting("Authentication:Jwt:SigningSecret", SigningSecret);
            builder.UseSetting("CoinsWallet:VoucherProtectionKey", ProtectorKey);
            builder.UseSetting("CoinsWallet:EnableSqlRewardWriter", "true");
            builder.UseSetting("CoinsWallet:EnableSqlVoucherRuntime", "true");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WayhunterSharedDb"] = connection,
                ["Identity:EnableSharedAuthentication"] = "true",
                ["Identity:AdministratorRole"] = "Admin",
                ["Authentication:Jwt:Issuer"] = Issuer,
                ["Authentication:Jwt:Audience"] = Audience,
                ["Authentication:Jwt:SigningSecret"] = SigningSecret,
                ["CoinsWallet:VoucherProtectionKey"] = ProtectorKey,
                ["CoinsWallet:EnableSqlRewardWriter"] = "true",
                ["CoinsWallet:EnableSqlVoucherRuntime"] = "true"
            }));
        });

        using var ownerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        using var strangerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(factory, owner, ownerSession, now));
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(factory, admin, adminSession, now));
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(factory, stranger, strangerSession, now));

        using var offerResponse = await adminClient.PostAsJsonAsync("/api/admin/coins-wallet/vouchers/offers",
            new CreateSampleVoucherOfferRequest(offerId, $"DEMO-{offerId:N}", now.AddDays(-1), now.AddDays(30)));
        Assert.Equal(HttpStatusCode.NoContent, offerResponse.StatusCode);
        using var inventoryResponse = await adminClient.PostAsJsonAsync("/api/admin/coins-wallet/vouchers/inventory",
            new AddSampleVoucherInventoryRequest(inventoryId, offerId, $"DEMO-CODE-{inventoryId:N}", "/private/demo-voucher-image", now.AddDays(20)));
        var inventoryBody = await inventoryResponse.Content.ReadAsStringAsync();
        Assert.True(inventoryResponse.StatusCode == HttpStatusCode.OK,
            $"Voucher inventory failed: {(int)inventoryResponse.StatusCode} {inventoryBody[..Math.Min(inventoryBody.Length, 600)]}");

        using var offers = await ownerClient.GetAsync("/api/mobile/coins-wallet/vouchers/offers");
        Assert.Equal(HttpStatusCode.OK, offers.StatusCode);
        var offerList = await offers.Content.ReadFromJsonAsync<SampleVoucherOfferResponse[]>();
        Assert.NotNull(offerList);
        Assert.Contains(offerList, x => x.OfferId == offerId && x.IsEligible && x.AvailableStock == 1);
        using var offerDetail = await ownerClient.GetAsync($"/api/mobile/coins-wallet/vouchers/offers/{offerId:D}");
        Assert.Equal(HttpStatusCode.OK, offerDetail.StatusCode);
        var offerDto = await offerDetail.Content.ReadFromJsonAsync<SampleVoucherOfferResponse>();
        Assert.NotNull(offerDto);
        Assert.Equal(offerId, offerDto.OfferId);
        Assert.Equal(1, offerDto.AvailableStock);

        var competingRequestId = Guid.NewGuid();
        var competingRequests = await Task.WhenAll(
            ownerClient.PostAsJsonAsync("/api/mobile/coins-wallet/vouchers/requests", new CreateSampleVoucherRequest(requestId, offerId)),
            ownerClient.PostAsJsonAsync("/api/mobile/coins-wallet/vouchers/requests", new CreateSampleVoucherRequest(competingRequestId, offerId)));
        Assert.Single(competingRequests, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(competingRequests, x => x.StatusCode == HttpStatusCode.Conflict);
        var acceptedRequestId = competingRequests[0].StatusCode == HttpStatusCode.OK ? requestId : competingRequestId;
        foreach (var competingResponse in competingRequests) competingResponse.Dispose();
        using var replay = await ownerClient.PostAsJsonAsync("/api/mobile/coins-wallet/vouchers/requests",
            new CreateSampleVoucherRequest(acceptedRequestId, offerId));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var pending = await replay.Content.ReadFromJsonAsync<SampleVoucherRequestResponse>();
        Assert.NotNull(pending);
        Assert.Equal(SampleVoucherRequestStatusDraft.Pending, pending.Status);

        using var reject = await adminClient.PostAsJsonAsync($"/api/admin/coins-wallet/vouchers/requests/{acceptedRequestId}/reject",
            new SampleVoucherDecisionRequest(Guid.NewGuid(), "Rejected for release-path verification"));
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var rejected = await reject.Content.ReadFromJsonAsync<SampleVoucherRequestResponse>();
        Assert.NotNull(rejected);
        Assert.Equal(SampleVoucherRequestStatusDraft.Rejected, rejected.Status);

        acceptedRequestId = Guid.NewGuid();
        using var replacementRequest = await ownerClient.PostAsJsonAsync("/api/mobile/coins-wallet/vouchers/requests",
            new CreateSampleVoucherRequest(acceptedRequestId, offerId));
        Assert.Equal(HttpStatusCode.OK, replacementRequest.StatusCode);

        using var insufficient = await ownerClient.PostAsJsonAsync("/api/mobile/coins-wallet/vouchers/requests",
            new CreateSampleVoucherRequest(Guid.NewGuid(), offerId));
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);

        using var approve = await adminClient.PostAsJsonAsync($"/api/admin/coins-wallet/vouchers/requests/{acceptedRequestId}/approve",
            new SampleVoucherDecisionRequest(Guid.NewGuid(), "Verified academic demo request"));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        using var issue = await adminClient.PostAsync($"/api/admin/coins-wallet/vouchers/requests/{acceptedRequestId}/issue", null);
        Assert.Equal(HttpStatusCode.OK, issue.StatusCode);
        using var issueReplay = await adminClient.PostAsync($"/api/admin/coins-wallet/vouchers/requests/{acceptedRequestId}/issue", null);
        Assert.Equal(HttpStatusCode.OK, issueReplay.StatusCode);

        using var wallet = await ownerClient.GetAsync("/api/mobile/coins-wallet/me");
        Assert.Equal(HttpStatusCode.OK, wallet.StatusCode);
        var walletJson = await wallet.Content.ReadAsStringAsync();
        Assert.Contains("200", walletJson, StringComparison.Ordinal);
        using var history = await ownerClient.GetAsync("/api/mobile/coins-wallet/me/transactions");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        using var issued = await ownerClient.GetAsync($"/api/mobile/coins-wallet/vouchers/requests/{acceptedRequestId}/issued-voucher");
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var issuedVoucher = await issued.Content.ReadFromJsonAsync<MyIssuedSampleVoucherResponse>();
        Assert.NotNull(issuedVoucher);
        Assert.False(string.IsNullOrWhiteSpace(issuedVoucher.VoucherCode));
        Assert.False(string.IsNullOrWhiteSpace(issuedVoucher.QrPayload));
        Assert.Equal("/private/demo-voucher-image", issuedVoucher.SampleImageReference);
        using var strangerAccess = await strangerClient.GetAsync($"/api/mobile/coins-wallet/vouchers/requests/{acceptedRequestId}/issued-voucher");
        Assert.Equal(HttpStatusCode.NotFound, strangerAccess.StatusCode);
        using var anonymousClient = factory.CreateClient();
        using var anonymousAccess = await anonymousClient.GetAsync($"/api/mobile/coins-wallet/vouchers/requests/{acceptedRequestId}/issued-voucher");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAccess.StatusCode);

        await using var verify = new WayhunterSharedDbContext(options);
        var persistedWallet = await verify.CoinWallets.SingleAsync(x => x.UserId == owner.UserId);
        Assert.Equal(200, persistedWallet.PostedCoins - persistedWallet.ReservedCoins);
        Assert.Equal(1, await verify.CoinLedgerEntries.CountAsync(x => x.WalletId == walletId && x.SourceRecordId == $"voucher-debit:{acceptedRequestId:D}"));
        Assert.Equal(1, await verify.SampleVoucherIssuances.CountAsync(x => x.RequestId == acceptedRequestId));
    }

    private static UserAccount CreateVerifiedUser(string name, DateTimeOffset now)
    {
        var user = UserAccount.RegisterPending(name, $"m6-{Guid.NewGuid():N}@example.invalid", "test-only-password-hash", now.AddMinutes(-2));
        user.ActivateAfterVerifiedOtp(now.AddMinutes(-1));
        return user;
    }

    private static string IssueToken(WebApplicationFactory<Program> factory, UserAccount user, IdentitySession session, DateTimeOffset now) =>
        factory.Services.GetRequiredService<SignedSessionTokenService>().Issue(user, session, now);

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("WAYHUNTER_M6_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("The M6 SQL test connection is missing.");
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
            !builder.InitialCatalog.StartsWith("Wayhunter_Test_", StringComparison.OrdinalIgnoreCase) || !builder.IntegratedSecurity)
            throw new InvalidOperationException("Only an authorized disposable LocalDB catalog is allowed.");
        return builder.ConnectionString;
    }
}
