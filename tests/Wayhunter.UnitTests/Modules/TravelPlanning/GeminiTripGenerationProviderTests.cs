
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GeminiTripGenerationProviderTests
{
    private const string ValidResponse = """
    {
      "candidates": [
        {
          "content": {
            "parts": [
              {
                "text": "{\"days\":[{\"date\":\"2026-11-20\",\"stops\":[{\"placeName\":\"Nallur\",\"estimatedCostLkr\":500}]}],\"estimatedTotalCostLkr\":500,\"requiresUserReview\":true}"
              }
            ]
          }
        }
      ]
    }
    """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }
        public Uri? Url { get; private set; }

        public HttpStatusCode Status { get; set; } =
            HttpStatusCode.OK;

        public string ResponseJson { get; set; } =
            ValidResponse;

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Calls++;
            Url = request.RequestUri;

            ApiKey = string.Join(
                "",
                request.Headers.GetValues("x-goog-api-key"));

            Body = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(
                    ResponseJson,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private static AiTripProviderOptions Options(
        bool enabled = true) => new()
        {
            Enabled = enabled,
            ApiKey = "test-key-not-real",
            Model = "test-model",
            TimeoutSeconds = 10,
            MaxRequestsPerUserPerDay = 10
        };

    private static AiTripItineraryRequest Request() => new(
        "Jaffna",
        "Nallur",
        new DateOnly(2026, 11, 20),
        new DateOnly(2026, 11, 21),
        10000m,
        "Car",
        2,
        null);

    private static GeminiTripGenerationProvider Create(
        FakeHandler handler,
        AiTripProviderOptions options)
    {
        return new GeminiTripGenerationProvider(
            new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public async Task ValidResponse_ReturnsItineraryJson()
    {
        var handler = new FakeHandler();
        var provider = Create(handler, Options());

        var result = await provider.GenerateItineraryJsonAsync(
            Request());

        Assert.Contains("Nallur", result);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("test-key-not-real", handler.ApiKey);
        Assert.Contains("responseMimeType", handler.Body!);
        Assert.DoesNotContain(
            "test-key-not-real", handler.Body!);
        Assert.DoesNotContain(
            "test-key-not-real", handler.Url!.ToString());
    }

    [Fact]
    public async Task DisabledProvider_DoesNotSendRequest()
    {
        var handler = new FakeHandler();
        var provider = Create(handler, Options(false));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GenerateItineraryJsonAsync(
                Request()));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task RateLimit_IsRejected()
    {
        var handler = new FakeHandler
        {
            Status = HttpStatusCode.TooManyRequests
        };

        var provider = Create(handler, Options());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GenerateItineraryJsonAsync(
                Request()));
    }

    [Fact]
    public async Task EmptyCandidates_AreRejected()
    {
        var handler = new FakeHandler
        {
            ResponseJson = "{\"candidates\":[]}"
        };

        var provider = Create(handler, Options());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GenerateItineraryJsonAsync(
                Request()));
    }

    [Fact]
    public async Task CancelledRequest_DoesNotSendRequest()
    {
        var handler = new FakeHandler();
        var provider = Create(handler, Options());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GenerateItineraryJsonAsync(
                Request(), cts.Token));

        Assert.Equal(0, handler.Calls);
    }
}
