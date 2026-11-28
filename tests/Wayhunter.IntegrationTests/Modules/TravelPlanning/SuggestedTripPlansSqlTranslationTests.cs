using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class SuggestedTripPlansSqlTranslationTests
{
    [Fact]
    public async Task ReaderQueries_TranslateBeforeOpeningSqlConnection()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=Wayhunter_Test_Translation;Integrated Security=True")
            .AddInterceptors(new StopAtConnectionOpening())
            .Options;

        await using var db = new WayhunterSharedDbContext(options);
        var reader = new SqlSuggestedTripPlansReader(db);

        // EF translates each production query before it tries to open a connection.
        await Assert.ThrowsAsync<SqlTranslationCompletedException>(
            () => reader.GetEligibleAsync("translation-test-user", 5));
        await Assert.ThrowsAsync<SqlTranslationCompletedException>(
            () => reader.GetEligibleByIdAsync("translation-test-user", Guid.NewGuid()));
    }

    private sealed class StopAtConnectionOpening : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            throw new SqlTranslationCompletedException();
        }
    }

    private sealed class SqlTranslationCompletedException : Exception;
}
