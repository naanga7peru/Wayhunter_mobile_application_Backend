using Microsoft.EntityFrameworkCore;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

internal static class HunterSqlTransaction
{
    public static Task<T> ExecuteAsync<T>(WayhunterSharedDbContext db,
        Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var retry = false;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(_ =>
        {
            if (retry)
                db.ChangeTracker.Clear();
            retry = true;
            return operation();
        }, cancellationToken);
    }
}
