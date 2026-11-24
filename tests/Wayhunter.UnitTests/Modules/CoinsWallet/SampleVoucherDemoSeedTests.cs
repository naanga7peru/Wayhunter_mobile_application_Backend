using Wayhunter.Infrastructure.Persistence.Seeding;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class SampleVoucherDemoSeedTests
{
    [Fact]
    public void OfferSeed_IsStableAndBoundedToAcademicDemoWindow()
    {
        var first = SampleVoucherDemoSeed.CreateOfferRequest();
        var second = SampleVoucherDemoSeed.CreateOfferRequest();

        Assert.Equal(first, second);
        Assert.Equal(SampleVoucherDemoSeed.OfferId, first.OfferId);
        Assert.Equal("ACADEMIC-DEMO-10000", first.OfferCode);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), first.ValidFromUtc);
        Assert.Equal(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), first.ValidUntilUtc);
    }

    [Fact]
    public void InventorySeed_UsesStableIdentityAndOperatorProvidedProtectedValues()
    {
        var suppliedCode = Guid.NewGuid().ToString("N");
        var first = SampleVoucherDemoSeed.CreateInventoryRequest(
            suppliedCode,
            "private://demo-voucher-image");
        var second = SampleVoucherDemoSeed.CreateInventoryRequest(
            suppliedCode,
            "private://demo-voucher-image");

        Assert.Equal(first, second);
        Assert.Equal(SampleVoucherDemoSeed.InventoryId, first.InventoryId);
        Assert.Equal(SampleVoucherDemoSeed.OfferId, first.OfferId);
        Assert.Equal(suppliedCode, first.VoucherCode);
        Assert.Equal("private://demo-voucher-image", first.SampleImageReference);
    }

    [Fact]
    public void InventorySeed_RejectsMissingProtectedValues()
    {
        Assert.Throws<ArgumentException>(() =>
            SampleVoucherDemoSeed.CreateInventoryRequest(" ", "private://image"));
        Assert.Throws<ArgumentException>(() =>
            SampleVoucherDemoSeed.CreateInventoryRequest("demo-code", " "));
    }
}
