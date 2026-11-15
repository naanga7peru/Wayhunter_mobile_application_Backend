
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Infrastructure.Integrations.Email;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class ResendOtpEmailSenderTests
{
    private static readonly OtpEmailContent Content =
        OtpEmailTemplate.Create(
            "Demo Traveler",
            "123456",
            5);

    private static ResendOtpEmailSender CreateSender(
        Func<HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> handler)
    {
        var client = new HttpClient(
            new FakeHttpHandler(handler));

        return new ResendOtpEmailSender(
            client,
            "test-only-api-key",
            "noreply@example.com");
    }

    [Fact]
    public async Task Success_ShouldReturnProviderMessageId()
    {
        var sender = CreateSender((request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);

            Assert.Equal(
                "https://api.resend.com/emails",
                request.RequestUri?.ToString());

            Assert.Equal(
                "Bearer",
                request.Headers.Authorization?.Scheme);

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"id":"email_test_123"}""")
                });
        });

        var result = await sender.SendAsync(
            "traveler@example.com",
            Content);

        Assert.True(result.Accepted);
        Assert.Equal(
            "email_test_123",
            result.ProviderMessageId);

        Assert.Null(result.FailureCode);
    }

    [Fact]
    public async Task Request_ShouldIncludeEmailContent()
    {
        var sender = CreateSender(async (request, token) =>
        {
            var body = await request.Content!
                .ReadAsStringAsync(token);

            using var json = JsonDocument.Parse(body);

            var root = json.RootElement;

            Assert.Equal(
                "noreply@example.com",
                root.GetProperty("from").GetString());

            Assert.Equal(
                "traveler@example.com",
                root.GetProperty("to")[0].GetString());

            Assert.Equal(
                Content.Subject,
                root.GetProperty("subject").GetString());

            Assert.Contains(
                "123456",
                root.GetProperty("html").GetString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"email_test_456"}""")
            };
        });

        var result = await sender.SendAsync(
            "traveler@example.com",
            Content);

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task HttpFailure_ShouldReturnFailureCode()
    {
        var sender = CreateSender((request, token) =>
            Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.Unauthorized)));

        var result = await sender.SendAsync(
            "traveler@example.com",
            Content);

        Assert.False(result.Accepted);

        Assert.Equal(
            "provider_http_401",
            result.FailureCode);
    }

    [Fact]
    public async Task InvalidJson_ShouldBeRejected()
    {
        var sender = CreateSender((request, token) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "invalid-json")
                }));

        var result = await sender.SendAsync(
            "traveler@example.com",
            Content);

        Assert.False(result.Accepted);

        Assert.Equal(
            "invalid_provider_response",
            result.FailureCode);
    }

    [Fact]
    public async Task MissingMessageId_ShouldBeRejected()
    {
        var sender = CreateSender((request, token) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"status":"ok"}""")
                }));

        var result = await sender.SendAsync(
            "traveler@example.com",
            Content);

        Assert.False(result.Accepted);

        Assert.Equal(
            "invalid_provider_response",
            result.FailureCode);
    }

    [Fact]
    public async Task NetworkFailure_ShouldReturnFailureCode()
    {
        var sender = CreateSender((request, token) =>
            throw new HttpRequestException(
                "Simulated network failure"));

        var result = await sender.SendAsync(
            "traveler@example.com",
            Content);

        Assert.False(result.Accepted);

        Assert.Equal(
            "provider_network_error",
            result.FailureCode);
    }

    [Fact]
    public async Task Timeout_ShouldReturnFailureCode()
    {
        var sender = CreateSender((request, token) =>
            throw new TaskCanceledException(
                "Simulated provider timeout"));

        var result = await sender.SendAsync(
            "traveler@example.com",
            Content);

        Assert.False(result.Accepted);

        Assert.Equal(
            "provider_timeout",
            result.FailureCode);
    }

    [Fact]
    public async Task InvalidRecipient_ShouldBeRejected()
    {
        var sender = CreateSender((request, token) =>
            throw new InvalidOperationException(
                "HTTP should not be called"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => sender.SendAsync(
                "not-an-email",
                Content));
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _handler;

        public FakeHttpHandler(
            Func<HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
