using System.Security.Cryptography;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class AesVoucherSecretProtectorTests
{
    private static AesVoucherSecretProtector Create() => new(
        Convert.ToBase64String(Enumerable.Range(1, 32)
            .Select(x => (byte)x).ToArray()));

    [Fact]
    public void Protect_RoundTrips_WithoutPersistingPlaintext()
    {
        var protector = Create();

        string protectedCode = protector.Protect("DEMO-12345");

        Assert.DoesNotContain("DEMO-12345", protectedCode);
        Assert.Equal("DEMO-12345", protector.Unprotect(protectedCode));
        Assert.Equal(64, protector.ComputeHash("DEMO-12345").Length);
    }

    [Fact]
    public void Protect_UsesUniqueNonce_AndRejectsTampering()
    {
        var protector = Create();
        string first = protector.Protect("DEMO-12345");
        string second = protector.Protect("DEMO-12345");
        byte[] tampered = Convert.FromBase64String(first);
        tampered[^1] ^= 1;

        Assert.NotEqual(first, second);
        Assert.ThrowsAny<CryptographicException>(() =>
            protector.Unprotect(Convert.ToBase64String(tampered)));
    }

    [Fact]
    public void Constructor_FailsClosed_ForMissingOrWeakKeys()
    {
        Assert.Throws<InvalidOperationException>(() => new AesVoucherSecretProtector(""));
        Assert.Throws<InvalidOperationException>(() => new AesVoucherSecretProtector(
            Convert.ToBase64String(new byte[16])));
    }
}
