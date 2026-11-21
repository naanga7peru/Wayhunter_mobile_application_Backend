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
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class GoogleRoutesRedirectHttpTests
{
    [Fact]
    public async Task Redirect_IsNotFollowedOrGivenTheApiKey()
    {
        const string testKey = "routes-redirect-test-key";
        var receivedInitialKey = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var redirectedRequests = 0;
        string? redirectedKey = null;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var server = builder.Build();
        server.MapPost("/directions/v2:computeRoutes", (HttpContext context) =>
        {
            receivedInitialKey.TrySetResult(context.Request.Headers["X-Goog-Api-Key"].ToString());
            return Results.Redirect("/redirect-target", preserveMethod: true);
        });
        server.MapPost("/redirect-target", (HttpContext context) =>
        {
            redirectedKey = context.Request.Headers["X-Goog-Api-Key"].ToString();
            Interlocked.Increment(ref redirectedRequests);
            return Results.Ok();
        });
        await server.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddTravelPlanningModule();
        services.Configure<MapsProviderOptions>(options =>
        {
            options.EnableRoutes = true;
            options.RoutesServerApiKey = testKey;
        });
        services.AddHttpClient<GoogleRoutesProvider>().AddHttpMessageHandler(
            () => new LoopbackRoutesHandler(new Uri(server.Urls.Single())));

        await using var provider = services.BuildServiceProvider();
        var routes = provider.GetRequiredService<GoogleRoutesProvider>();
        var result = await routes.CalculateRouteAsync(
            [new RouteWaypoint(9.66, 80.02), new RouteWaypoint(9.67, 80.03)]);

        Assert.True(receivedInitialKey.Task.IsCompletedSuccessfully);
        Assert.Equal(testKey, await receivedInitialKey.Task);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, Volatile.Read(ref redirectedRequests));
        Assert.Null(redirectedKey);
    }

    // Replace only the destination. Keep the production primary handler so
    // the test exercises its actual redirect behavior.
    private sealed class LoopbackRoutesHandler(Uri address) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new Uri(address, request.RequestUri!.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
