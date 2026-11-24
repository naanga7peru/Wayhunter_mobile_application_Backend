using Wayhunter.DemoSeedRunner;
using Microsoft.AspNetCore.Identity;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.DemoSeedRunner;

public sealed class DemoSeedRunnerSafetyTests
{
    private const string Catalog = "Wayhunter_Demo_20261124";

    [Fact]
    public void OptionsRequireExactLocalDemoCatalogAndExplicitAuthorization()
    {
        var options = DemoSeedOptions.FromEnvironment(Environment(
            $"SEED_DEMO_DATABASE:{Catalog}"));
        Assert.Equal(Catalog, options.Catalog);
        Assert.Contains(Catalog, options.ConnectionString);
    }

    [Theory]
    [InlineData("Wayhunter_Official")]
    [InlineData("Wayhunter_Production")]
    [InlineData("Wayhunter_Test_M5_eee96080bb0540ebaa5cd326f366d2fb")]
    public void OptionsRefuseNonDemoCatalogs(string catalog)
    {
        Assert.Throws<InvalidOperationException>(() => DemoSeedOptions.FromEnvironment(
            Environment($"SEED_DEMO_DATABASE:{catalog}", catalog)));
    }

    [Fact]
    public void OptionsRefuseCatalogMismatchAndMissingAuthorization()
    {
        var mismatch = Environment($"SEED_DEMO_DATABASE:{Catalog}");
        mismatch["WAYHUNTER_DEMO_SEED_CONNECTION"] =
            @"Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Demo_Other;Integrated Security=true;TrustServerCertificate=true";
        Assert.Throws<InvalidOperationException>(() => DemoSeedOptions.FromEnvironment(mismatch));

        var missing = Environment("wrong");
        Assert.Throws<InvalidOperationException>(() => DemoSeedOptions.FromEnvironment(missing));
    }

    [Fact]
    public void DemoIdentityAndDataIdentifiersAreStableAndUnique()
    {
        Assert.Equal(10, DemoUsers.All.Count);
        Assert.Equal(10, DemoUsers.All.Select(x => x.Id).Distinct().Count());
        Assert.Equal(10, DemoUsers.All.Select(x => x.Email).Distinct().Count());
        Assert.Equal(9, DemoUsers.All.Count(x => !x.IsAdmin));
        Assert.Equal(1, DemoUsers.All.Count(x => x.IsAdmin));
        Assert.Equal(10, DemoUsers.All.Select(x => DemoSeedIds.WelcomeReward(x.Id)).Distinct().Count());
        Assert.Equal(10, Enumerable.Range(0, 5).SelectMany(i => Enumerable.Range(0, 2)
            .Select(s => DemoSeedIds.Stop(i, s))).Distinct().Count());
    }

    [Fact]
    public void DemoPasswordUsesRuntimeCompatibleAspNetIdentityHashing()
    {
        const string password = "demo-password-secret";
        var hasher = new PasswordHasher<object>();
        var hash = hasher.HashPassword(new object(), password);
        Assert.NotEqual(password, hash);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(new object(), hash, password));

        var seed = DemoUsers.All[0];
        var now = new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
        var account = UserAccount.RegisterPendingWithId(seed.Id, seed.Name, seed.Email,
            hash, now);
        account.ActivateAfterVerifiedOtp(now.AddMinutes(1));
        var login = new LoginCredentialValidationService(hasher);
        Assert.True(login.Validate(account, password));
        Assert.False(login.Validate(account, "wrong-password"));
    }

    private static Dictionary<string, string?> Environment(string confirmation, string? catalog = null) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["WAYHUNTER_DEMO_SEED_CONNECTION"] = $@"Server=(localdb)\MSSQLLocalDB;Database={catalog ?? Catalog};Integrated Security=true;TrustServerCertificate=true",
            ["WAYHUNTER_DEMO_SEED_DATABASE"] = catalog ?? Catalog,
            ["WAYHUNTER_DEMO_SEED_CONFIRM"] = confirmation,
            ["WAYHUNTER_DEMO_ACCOUNT_PASSWORD"] = "demo-pass-12345",
            ["WAYHUNTER_DEMO_VOUCHER_CODE"] = "SAMPLE-CODE-123",
            ["WAYHUNTER_DEMO_VOUCHER_IMAGE_REFERENCE"] = "private-demo-voucher-image",
            ["WAYHUNTER_DEMO_VOUCHER_PROTECTION_KEY"] = Convert.ToBase64String(new byte[32])
        };
}
