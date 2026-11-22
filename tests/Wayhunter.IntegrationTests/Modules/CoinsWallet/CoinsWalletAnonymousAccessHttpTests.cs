using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.CoinsWallet;

public sealed class CoinsWalletAnonymousAccessHttpTests
{
    [Theory]
    [InlineData("GET", "/api/mobile/coins-wallet/me")]
    [InlineData("GET", "/api/mobile/coins-wallet/me/transactions")]
    [InlineData("GET", "/api/mobile/coins-wallet/vouchers/offers")]
    [InlineData("POST", "/api/mobile/coins-wallet/vouchers/requests")]
    public async Task MobileWalletRoutes_RejectAnonymousRequests(
        string method,
        string route)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/coins-wallet/vouchers/requests")]
    [InlineData("POST", "/api/admin/coins-wallet/vouchers/requests/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("POST", "/api/admin/coins-wallet/ledger/00000000-0000-0000-0000-000000000001/reverse")]
    public async Task AdminWalletRoutes_RejectAnonymousRequests(
        string method,
        string route)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
