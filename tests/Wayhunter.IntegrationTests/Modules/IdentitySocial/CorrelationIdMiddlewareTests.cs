
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Wayhunter.Api.Contracts;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed class CorrelationIdMiddlewareTests
{
    private const string Header =
        IdentityApiContract.CorrelationIdHeader;

    [Fact]
    public async Task MissingHeader_ShouldGenerateCorrelationId()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(response.Headers.TryGetValues(
            Header, out var values));

        string correlationId = Assert.Single(values);

        Assert.True(Guid.TryParseExact(
            correlationId, "N", out _));
    }

    [Fact]
    public async Task ValidHeader_ShouldBeReturned()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        const string expectedId = "wayhunter-test-123";

        client.DefaultRequestHeaders.Add(Header, expectedId);

        using var response = await client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues(
            Header, out var values));

        Assert.Equal(expectedId, Assert.Single(values));
    }

    [Fact]
    public async Task InvalidHeader_ShouldBeReplaced()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/health");

        request.Headers.TryAddWithoutValidation(
            Header, "invalid correlation id");

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(
            Header, out var values));

        string correlationId = Assert.Single(values);

        Assert.NotEqual(
            "invalid correlation id", correlationId);

        Assert.True(Guid.TryParseExact(
            correlationId, "N", out _));
    }

    [Fact]
    public async Task SeparateRequests_ShouldGenerateDifferentIds()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/health");
        using var second = await client.GetAsync("/health");

        string firstId = Assert.Single(
            first.Headers.GetValues(Header));

        string secondId = Assert.Single(
            second.Headers.GetValues(Header));

        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task NotFoundResponse_ShouldContainCorrelationId()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/route-that-does-not-exist");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.True(response.Headers.TryGetValues(
            Header, out var values));

        Assert.False(string.IsNullOrWhiteSpace(
            Assert.Single(values)));
    }
}
