using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.Games;

public sealed class M5DraftRoutesApprovalGateHttpTests
{
    [Fact]
    public async Task ProposedDraftRoutes_RemainUnmappedUntilContractApproval()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var gameId = Guid.NewGuid();
        var requests = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Post, "/api/mobile/games/drafts"),
            (HttpMethod.Get, $"/api/mobile/games/drafts/{gameId:D}"),
            (HttpMethod.Put, $"/api/mobile/games/drafts/{gameId:D}"),
            (HttpMethod.Delete, $"/api/mobile/games/drafts/{gameId:D}")
        };

        foreach (var (method, path) in requests)
        {
            using var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post || method == HttpMethod.Put)
                request.Content = new StringContent("{}");

            using var response = await client.SendAsync(request);
            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"Draft route must remain unmapped: {method} {path}; actual: {response.StatusCode}");
        }
    }
}
