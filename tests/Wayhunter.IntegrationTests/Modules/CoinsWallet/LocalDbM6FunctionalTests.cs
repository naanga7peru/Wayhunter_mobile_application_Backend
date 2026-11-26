using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed partial class LocalDbAuthenticationPhase3Tests
{
    [LocalDbM6Fact]
    public Task VerifyM6Operations()
    {
        var catalog = Environment.GetEnvironmentVariable("WAYHUNTER_M6_FUNCTIONAL_CATALOG");
        Assert.True(catalog is not null && Regex.IsMatch(catalog, @"\AWayhunter_M6_Functional_[a-f0-9]{32}\z"));
        var fault = new M6ApprovalCommitFault();
        return RunIsolatedAsync(catalog!, migrateFreshCatalog: true, (client, db, capture, clock, _) =>
        {
            clock.Offset = TimeSpan.Zero;
            Assert.False(db.Database.HasPendingModelChanges());
            return VerifyCoinsAsync(client, db, capture, clock, fault);
        }, services => services.ConfigureDbContext<WayhunterSharedDbContext>(options => options.AddInterceptors(fault)),
            additionalSettings: new Dictionary<string, string?>
            {
                ["CoinsWallet:VoucherProtectionKey"] = Environment.GetEnvironmentVariable("CoinsWallet__VoucherProtectionKey")
            });
    }

    private async Task VerifyCoinsAsync(HttpClient client, WayhunterSharedDbContext db, Capture capture,
        TestClock clock, M6ApprovalCommitFault fault)
    {
        const string mobile = "/api/mobile/coins-wallet";
        const string admin = "/api/admin/coins-wallet";
        const string offers = mobile + "/vouchers/offers";
        const string requests = mobile + "/vouchers/requests";
        const string queue = admin + "/vouchers/requests";
        const string rules = admin + "/rule-versions";
        var now = DateTimeOffset.UtcNow;
        var offer = Guid.NewGuid();
        var inventory = Guid.NewGuid();
        var request = Guid.NewGuid();
        var ledger = Guid.NewGuid();
        var wallet = Guid.NewGuid();
        var item = $"{requests}/{request}";
        var review = $"{queue}/{request}";
        var correction = new ReverseCoinLedgerEntryRequest(Guid.NewGuid(), "Fixture correction", "fixture");
        var rule = new CreateCoinRuleVersionRequest(Guid.NewGuid(), "fixture-v1", now.AddDays(-1), null);
        var decision = new SampleVoucherDecisionRequest(Guid.NewGuid(), "Fixture review");
        var create = new CreateSampleVoucherRequest(request, offer);
        var code = $"FIXTURE-{Guid.NewGuid():N}";
        (string Method, string Path, object? Body)[] operations =
        [
            ("GET", mobile + "/me", null), ("GET", mobile + "/me/transactions", null),
            ("GET", offers, null), ("GET", $"{offers}/{offer}", null),
            ("POST", requests, create), ("GET", item, null),
            ("POST", item + "/cancel", new CancelSampleVoucherRequest("Fixture cancellation")), ("GET", item + "/issued-voucher", null),
            ("POST", $"{admin}/ledger/{ledger}/reverse", correction),
            ("GET", admin + "/reports/operations", null), ("GET", admin + "/reports/reconciliation", null),
            ("GET", rules, null), ("POST", rules, rule), ("POST", admin + "/vouchers/expiry/run", null),
            ("POST", admin + "/vouchers/inventory", new AddSampleVoucherInventoryRequest(inventory, offer, code, "/private/fixture", now.AddMinutes(10))),
            ("POST", admin + "/vouchers/offers", new CreateSampleVoucherOfferRequest(offer, "Fixture offer", now.AddDays(-1), now.AddMinutes(15))),
            ("GET", queue, null), ("POST", review + "/approve", decision),
            ("POST", review + "/issue", null), ("POST", review + "/reject", decision),
            ("POST", review + "/use", new ConsumeSampleVoucherRequest("Fixture use"))
        ];
        foreach (var operation in operations)
        {
            await Check(operation, 401);
            await Check(operation, 401, "invalid.jwt.token");
        }

        var owner = await Register("Voucher traveler");
        var stranger = await Register("Other traveler");
        var reviewer = await Register("Fixture administrator");
        db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(reviewer.Id, now, null));
        db.CoinWallets.Add(new CoinWalletRow
        {
            Id = wallet, UserId = owner.Id, PostedCoins = 10_500, LifetimeEarnedCoins = 10_500
        });
        db.CoinLedgerEntries.AddRange(
            new CoinLedgerEntryRow
            {
                Id = Guid.NewGuid(), WalletId = wallet, DeltaCoins = 10_400, SourceRecordId = "fixture-seed",
                RuleVersion = "fixture", CorrelationId = Guid.NewGuid(), RecordedAtUtc = now
            },
            new CoinLedgerEntryRow
            {
                Id = ledger, WalletId = wallet, DeltaCoins = 100, SourceRecordId = "fixture-adjustment",
                RuleVersion = "fixture", CorrelationId = Guid.NewGuid(), RecordedAtUtc = now
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        for (var index = 8; index < operations.Length; index++)
            await Check(operations[index], 403, owner.Token);

        await Check(operations[0], 200, owner.Token);
        await Check(("GET", mobile + "/me?userId=" + owner.Id, null), 404, stranger.Token);
        await Check(operations[1], 200, owner.Token);
        await Check(("GET", mobile + "/me/transactions?page=0", null), 400, owner.Token);
        await Check(operations[12], 200, reviewer.Token);
        await Check(operations[11], 200, reviewer.Token);
        await Check(operations[15], 204, reviewer.Token);
        await Check(operations[14], 200, reviewer.Token);
        await Check(operations[2], 200, owner.Token);
        await Check(operations[3], 200, owner.Token);
        await Check(("GET", $"{offers}/{Guid.NewGuid()}", null), 404, owner.Token);
        await Check(operations[4], 409, stranger.Token);
        await Check(("POST", requests, create with { OfferId = Guid.Empty }), 400, owner.Token);
        await Task.WhenAll(Check(operations[4], 200, owner.Token), Check(operations[4], 200, owner.Token));
        await Check(("POST", requests, create with { RequestId = Guid.NewGuid() }), 409, owner.Token);
        await AssertWallet(10_500, 10_000);
        await Check(operations[5], 200, owner.Token);
        await Check(operations[5], 404, stranger.Token);
        await Check(operations[16], 200, reviewer.Token);
        await Check(operations[6], 200, owner.Token);
        await Check(operations[6], 200, owner.Token);
        await AssertWallet(10_500, 0);

        var rejected = Guid.NewGuid();
        await Check(("POST", requests, create with { RequestId = rejected }), 200, owner.Token);
        await Check(("POST", $"{queue}/{rejected}/reject", decision), 200, reviewer.Token);
        await AssertWallet(10_500, 0);

        var approved = Guid.NewGuid();
        await Check(("POST", requests, create with { RequestId = approved }), 200, owner.Token);
        var approve = ("POST", $"{queue}/{approved}/approve", (object?)decision);
        fault.Arm();
        await Check(approve, 409, reviewer.Token);
        Assert.False(fault.Armed);
        await AssertWallet(10_500, 10_000);
        Assert.False(await db.CoinLedgerEntries.AnyAsync(x => x.DeltaCoins < 0));
        Assert.Equal(SampleVoucherRequestStatusDraft.Pending,
            await db.SampleVoucherRedemptionRequests.Where(x => x.Id == approved).Select(x => x.Status).SingleAsync());
        await Task.WhenAll(Check(approve, 200, reviewer.Token), Check(approve, 200, reviewer.Token));
        await AssertWallet(500, 0);
        var issue = ("POST", $"{queue}/{approved}/issue", (object?)null);
        await Task.WhenAll(Check(issue, 200, reviewer.Token), Check(issue, 200, reviewer.Token));
        var issued = ("GET", $"{requests}/{approved}/issued-voucher", (object?)null);
        await Check(issued, 404, stranger.Token);
        var voucher = await Check(issued, 200, owner.Token, allowCode: true);
        Assert.Equal(code, voucher.GetProperty("voucherCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(voucher.GetProperty("qrPayload").GetString()));
        var use = ("POST", $"{queue}/{approved}/use", new ConsumeSampleVoucherRequest("Fixture use"));
        await Check(use, 200, reviewer.Token);
        await Check(use, 200, reviewer.Token);
        await Check(operations[13], 200, reviewer.Token);
        await Check(operations[8], 200, reviewer.Token);
        await Check(operations[8], 200, reviewer.Token);
        await Check(operations[9], 200, reviewer.Token);
        await Check(operations[10], 200, reviewer.Token);
        await AssertWallet(400, 0);
        Assert.Equal(1, await db.SampleVoucherIssuances.CountAsync());
        Assert.Equal(1, await db.CoinLedgerEntries.CountAsync(x => x.DeltaCoins == -10_000));
        Assert.Equal(1, await db.CoinLedgerEntries.CountAsync(x => x.DeltaCoins == -100));
        Assert.Equal(SampleVoucherIssueStatusDraft.Used, await db.SampleVoucherIssuances.Select(x => x.Status).SingleAsync());
        clock.Offset = TimeSpan.FromMinutes(16);
        await Check(operations[3], 404, owner.Token);

        async Task AssertWallet(long posted, long reserved)
        {
            var current = await db.CoinWallets.AsNoTracking().SingleAsync(x => x.Id == wallet);
            Assert.Equal(posted, current.PostedCoins);
            Assert.Equal(reserved, current.ReservedCoins);
            Assert.Equal(posted, await db.CoinLedgerEntries.Where(x => x.WalletId == wallet).SumAsync(x => x.DeltaCoins));
        }

        async Task<JsonElement> Check((string Method, string Path, object? Body) operation, int expected, string? token = null, bool allowCode = false)
        {
            using var message = new HttpRequestMessage(new HttpMethod(operation.Method), operation.Path);
            if (token is not null)
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (operation.Body is not null)
                message.Content = JsonContent.Create(operation.Body);
            using var response = await client.SendAsync(message);
            var content = await response.Content.ReadAsStringAsync();
            output.WriteLine($"{operation.Method} {operation.Path} expected {expected}, actual {(int)response.StatusCode}");
            Assert.Equal(expected, (int)response.StatusCode);
            if (!allowCode)
                Assert.False(content.Contains(code, StringComparison.Ordinal), "Voucher code disclosed outside owner retrieval.");
            if (string.IsNullOrWhiteSpace(content))
                return default;
            using var json = JsonDocument.Parse(content);
            return json.RootElement.Clone();
        }

        async Task<(string Id, string Token)> Register(string name)
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            var password = Guid.NewGuid().ToString("N") + "A!";
            using var registration = await client.PostAsJsonAsync("/api/mobile/identity/register",
                new { displayName = name, email, password, confirmPassword = password });
            Assert.Equal(201, (int)registration.StatusCode);
            using var verify = await client.PostAsJsonAsync("/api/mobile/identity/verify-otp", new { email, code = capture.Codes.Last() });
            Assert.Equal(204, (int)verify.StatusCode);
            using var signIn = await client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
            Assert.Equal(200, (int)signIn.StatusCode);
            using var data = (await signIn.Content.ReadFromJsonAsync<JsonDocument>())!;
            return (data.RootElement.GetProperty("userId").GetString()!, data.RootElement.GetProperty("accessToken").GetString()!);
        }
    }

    private sealed class M6ApprovalCommitFault : DbTransactionInterceptor
    {
        private int _armed;
        public bool Armed => Volatile.Read(ref _armed) != 0;
        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) != 0)
                throw new InvalidOperationException("Synthetic M6 approval pre-commit failure.");
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }
}

public sealed class LocalDbM6FactAttribute : FactAttribute
{
    public LocalDbM6FactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M6_FUNCTIONAL_CATALOG")))
            Skip = "Requires a fresh isolated WayhunterTest2026 catalog.";
    }
}
