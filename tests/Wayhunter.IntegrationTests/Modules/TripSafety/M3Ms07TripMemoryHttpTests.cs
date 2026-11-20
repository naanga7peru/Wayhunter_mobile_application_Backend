using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3Ms07TripMemoryHttpTests
{
    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("conflicting", HttpStatusCode.Unauthorized)]
    [InlineData("trusted", HttpStatusCode.ServiceUnavailable)]
    public async Task ProductionDefaultsRejectUntrustedOrUnavailableOperations(string identity, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(TripMemoriesController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Handler>("Test", _ => { });
        builder.Services.AddSingleton<ITripPlanRepository, InMemoryTripPlanRepository>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTripMemoryServices();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
        var path = $"/api/mobile/trips/{Guid.NewGuid():D}/memories";
        var memoryPath = $"{path}/{Guid.NewGuid():D}";

        using var create = await client.PostAsJsonAsync(path, new { operationId = Guid.NewGuid(), caption = "Private" });
        using var list = await client.GetAsync(path);
        using var get = await client.GetAsync(memoryPath);
        using var deleted = await client.DeleteAsync(memoryPath);
        using var upload = await client.PostAsJsonAsync($"{memoryPath}/media/uploads",
            new { operationId = Guid.NewGuid(), contentType = "image/png", contentLength = 128 });
        using var complete = await client.PostAsync($"{memoryPath}/media/uploads/{Guid.NewGuid():D}/complete", null);

        Assert.All(new[] { create, list, get, deleted, upload, complete },
            response => Assert.Equal(expected, response.StatusCode));
        Assert.DoesNotContain(app.Services.GetServices<IHostedService>(),
            worker => worker.GetType().Name.Contains("TripMemory", StringComparison.Ordinal));

        var provider = app.Services.GetRequiredService<ITripMemoryMediaProvider>();
        var operation = new TripMemoryMedia(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("D"),
            "image/png", 128, DateTimeOffset.UtcNow, TripMemoryMediaState.Pending);
        await Assert.ThrowsAsync<TripMemoryUnavailableException>(() => provider.IssueAsync(operation, default));
        var asset = new VerifiedMemoryAsset(operation.OperationId, operation.MemoryId, operation.OwnerUserId,
            "test", "v1", operation.ContentType, operation.ContentLength, true, true);
        await Assert.ThrowsAsync<TripMemoryUnavailableException>(() => provider.DeleteVerifiedVersionAsync(asset, default));
    }

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var kind = Request.Headers["X-Test-Identity"].ToString();
            if (kind == "anonymous")
                return Task.FromResult(AuthenticateResult.NoResult());

            var user = Guid.NewGuid().ToString("D");
            var subject = kind == "conflicting" ? Guid.NewGuid().ToString("D") : user;
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user), new Claim("sub", subject)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
