
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SessionAuthenticationRegistrationTests
{
    private const string TestSecret =
        "test-only-secret-at-least-32-characters-long-123";

    private static IConfiguration CreateConfiguration(
        string? issuer = "wayhunter-test",
        string? audience = "wayhunter-mobile-test",
        string? secret = TestSecret)
    {
        var values = new Dictionary<string, string?>();

        if (issuer is not null)
            values["Authentication:Jwt:Issuer"] = issuer;

        if (audience is not null)
            values["Authentication:Jwt:Audience"] = audience;

        if (secret is not null)
            values["Authentication:Jwt:SigningSecret"] = secret;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void ValidConfiguration_RegistersJwtServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        services.AddWayhunterSessionAuthentication(
            CreateConfiguration());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(
            provider.GetRequiredService<SignedSessionTokenService>());

        Assert.NotNull(
            provider.GetRequiredService<
                IAuthenticationSchemeProvider>());
    }

    [Fact]
    public void MissingIssuer_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddWayhunterSessionAuthentication(
                CreateConfiguration(issuer: null)));
    }

    [Fact]
    public void MissingAudience_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddWayhunterSessionAuthentication(
                CreateConfiguration(audience: null)));
    }

    [Fact]
    public void MissingSigningSecret_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddWayhunterSessionAuthentication(
                CreateConfiguration(secret: null)));
    }

    [Fact]
    public void ShortSigningSecret_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddWayhunterSessionAuthentication(
                CreateConfiguration(secret: "short")));
    }
}
