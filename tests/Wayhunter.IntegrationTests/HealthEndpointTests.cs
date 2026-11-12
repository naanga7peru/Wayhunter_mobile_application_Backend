using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Wayhunter.IntegrationTests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Get_Health_Returns_Ok()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
