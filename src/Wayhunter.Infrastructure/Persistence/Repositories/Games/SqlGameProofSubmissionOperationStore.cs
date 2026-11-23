using System;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5 durable proof operation store. It remains unregistered in production;
/// construction is limited to uniquely named disposable M5 LocalDB catalogs.
/// </summary>
public sealed partial class SqlGameProofSubmissionOperationStore : IGameProofSubmissionOperationStore
{
    private readonly string _connectionString;

    public SqlGameProofSubmissionOperationStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQL connection string is required.", nameof(connectionString));

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new ArgumentException("SQL server and database are required.", nameof(connectionString));

        // This store is intentionally restricted to disposable test catalogs.
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(builder.InitialCatalog, @"^Wayhunter_Test_M5_[0-9a-fA-F]{32}$", RegexOptions.CultureInvariant) ||
            string.Equals(builder.InitialCatalog, "Wayhunter_Official", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
            throw new InvalidOperationException("Only a uniquely named disposable M5 LocalDB catalog is allowed.");
        _connectionString = builder.ConnectionString;
    }

    private SqlConnection CreateConnection() => new(_connectionString);
}
