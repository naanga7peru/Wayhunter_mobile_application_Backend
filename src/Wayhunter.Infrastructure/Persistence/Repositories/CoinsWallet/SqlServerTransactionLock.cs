using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

internal static class SqlServerTransactionLock
{
    public static async Task AcquireAsync(
        WayhunterSharedDbContext db,
        string category,
        string key,
        CancellationToken cancellationToken)
    {
        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        string resource = $"m6:{category}:{hash}";

        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A SQL transaction is required.");

        DbConnection connection = db.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
        DECLARE @lockResult int;
        EXEC @lockResult = sys.sp_getapplock
            @Resource = @resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 15000;
        SELECT @lockResult;
        """;

        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);

        object? result = await command.ExecuteScalarAsync(
            cancellationToken);

        if (Convert.ToInt32(result) < 0)
            throw new InvalidOperationException(
                "Could not acquire M6 transaction lock.");
    }
}
