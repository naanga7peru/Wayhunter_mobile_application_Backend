using Wayhunter.Domain;
using Xunit;

namespace Wayhunter.UnitTests;

public class StarterSmokeTests
{
    [Fact]
    public void Domain_Assembly_Is_Available()
        => Assert.NotNull(typeof(DomainAssemblyMarker).Assembly);
}
