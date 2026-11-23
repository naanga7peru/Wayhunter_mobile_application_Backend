using System;
using System.Threading.Tasks;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class SqlGameProofSubmissionOperationStoreGuardTests
{
    private const string Allowed = @"Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Test_M5_00000000000000000000000000000000;Integrated Security=True";

    [Fact]
    public void ApprovedLocalDb_ShouldBeAccepted()
    {
        _ = new SqlGameProofSubmissionOperationStore(Allowed);
    }

    [Theory]
    [InlineData("Server=localhost;Database=Wayhunter_Test_M5_00000000000000000000000000000000;Integrated Security=True")]
    [InlineData(@"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True")]
    [InlineData(@"Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Test_M5_00000000000000000000000000000000;AttachDbFilename=C:\temp\other.mdf;Integrated Security=True")]
    [InlineData(@"Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Test_M5_wrong;Integrated Security=True")]
    public void NonApprovedConnection_ShouldReject(string connection)
    {
        Assert.Throws<InvalidOperationException>(() => new SqlGameProofSubmissionOperationStore(connection));
    }

    [Fact]
    public async Task EmptyOperationId_ShouldRejectBeforeSqlAccess()
    {
        var store = new SqlGameProofSubmissionOperationStore(Allowed);
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync(Guid.Empty));
    }
}
