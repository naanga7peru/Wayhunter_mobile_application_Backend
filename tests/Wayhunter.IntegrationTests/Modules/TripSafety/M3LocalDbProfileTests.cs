using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3LocalDbProfileTests
{
    private const string Catalog = "Wayhunter_Test_M3_0123456789abcdef0123456789abcdef";
    private const string Local = @"Server=(localdb)\MSSQLLocalDB;Database=" + Catalog + ";Integrated Security=True";

    [Fact]
    public void ExactDedicatedProfileIsReconstructedWithoutOpeningConnection()
    {
        var result = M3LocalDbProfile.Validate(Local + ";Connect Timeout=3", Catalog);
        Assert.Equal(Catalog, result.InitialCatalog);
        Assert.True(result.IntegratedSecurity);
        Assert.Equal(15, result.ConnectTimeout);
    }

    [Theory]
    [InlineData("Server=remote;Database=" + Catalog + ";Integrated Security=True")]
    [InlineData(Local + ";Database=Other")]
    [InlineData(Local + ";AttachDbFilename=C:\\example.mdf")]
    [InlineData(Local + ";Failover Partner=other")]
    [InlineData(Local + ";User ID=unexpected")]
    [InlineData(Local + ";Integrated Security=False")]
    public void UnexpectedDestinationOrOptionFailsBeforeConnection(string value) =>
        Assert.Throws<InvalidOperationException>(() => M3LocalDbProfile.Validate(value, Catalog));
}
