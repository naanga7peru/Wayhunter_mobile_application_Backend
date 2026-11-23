using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Application.Modules.TripSafety.Services;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class TripSafeCheckInHttpTests
{
    [Theory]
    [InlineData("anonymous", false, false, HttpStatusCode.Unauthorized)]
    [InlineData("invalid", true, true, HttpStatusCode.Unauthorized)]
    [InlineData("conflicting", true, true, HttpStatusCode.Unauthorized)]
    [InlineData("valid", false, false, HttpStatusCode.ServiceUnavailable)]
    [InlineData("valid", true, false, HttpStatusCode.ServiceUnavailable)]
    [InlineData("valid", true, true, HttpStatusCode.ServiceUnavailable)]
    public async Task CheckIn_RejectsUntrustedIdentityOrReturnsUnavailableWithoutSideEffects(
        string identity, bool enableCheckIn, bool enableSql, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TripSafety:EnableSafeCheckIn"] = enableCheckIn ? "true" : null,
            ["TripSafety:EnableSqlPersistence"] = enableSql ? "true" : null
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(TripSafeCheckInController).Assembly);
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, CheckInAuthenticationHandler>("Test", _ => { });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
        using var response = await client.PostAsync(
            $"/api/mobile/trips/{Guid.NewGuid():D}/sessions/{Guid.NewGuid():D}/im-safe", null);

        Assert.Equal(expected, response.StatusCode);
    }

    private sealed class CheckInAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var kind = Request.Headers["X-Test-Identity"].ToString();
            if (kind == "anonymous")
                return Task.FromResult(AuthenticateResult.NoResult());

            var userId = Guid.NewGuid().ToString("D");
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, kind == "invalid" ? "invalid" : userId),
                new Claim("sub", kind == "conflicting" ? Guid.NewGuid().ToString("D") : userId)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
