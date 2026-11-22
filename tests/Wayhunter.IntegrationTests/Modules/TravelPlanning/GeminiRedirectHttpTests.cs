using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class GeminiRedirectHttpTests
{
    [Fact]
    public async Task Redirect_IsNotFollowedOrGivenTheApiKey()
    {
        const string testKey = "gemini-redirect-test-key";
        var receivedInitialKey = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var redirectedRequests = 0;
        string? redirectedKey = null;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var server = builder.Build();
        server.MapPost(
            "/v1beta/models/gemini-test:generateContent",
            (HttpContext context) =>
            {
                receivedInitialKey.TrySetResult(
                    context.Request.Headers["x-goog-api-key"].ToString());
                return Results.Redirect(
                    "/redirect-target",
                    preserveMethod: true);
            });
        server.Map("/redirect-target", (HttpContext context) =>
        {
            redirectedKey = context.Request.Headers["x-goog-api-key"].ToString();
            Interlocked.Increment(ref redirectedRequests);
            return Results.Ok();
        });
        await server.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().Build());
        services.AddTravelPlanningModule();
        services.Configure<AiTripProviderOptions>(options =>
        {
            options.Enabled = true;
            options.ApiKey = testKey;
            options.Model = "gemini-test";
        });
        services.AddHttpClient<
                IAiTripGenerationProvider,
                GeminiTripGenerationProvider>()
            .AddHttpMessageHandler(
                () => new LoopbackGeminiHandler(
                    new Uri(server.Urls.Single())));

        await using var provider = services.BuildServiceProvider();
        var gemini = provider.GetRequiredService<IAiTripGenerationProvider>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gemini.GenerateItineraryJsonAsync(
                new AiTripItineraryRequest(
                    "Jaffna",
                    "Point Pedro",
                    new DateOnly(2026, 12, 1),
                    new DateOnly(2026, 12, 2),
                    10000m,
                    "car",
                    2,
                    null)));

        Assert.True(receivedInitialKey.Task.IsCompletedSuccessfully);
        Assert.Equal(testKey, await receivedInitialKey.Task);
        Assert.Equal(0, Volatile.Read(ref redirectedRequests));
        Assert.Null(redirectedKey);
    }

    // Replace only the destination. Keep the production primary handler so
    // the test exercises its actual redirect behavior.
    private sealed class LoopbackGeminiHandler(Uri address) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.RequestUri = new Uri(
                address,
                request.RequestUri!.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
