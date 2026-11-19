using System;
using Microsoft.Data.SqlClient;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// Isolated M5 durable proof operation store foundation.
/// No production DI registration or shared migrations.
/// SQL operations will be implemented and verified separately.
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

        // Temporary test-only guard until DB Coordinator approval.
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(builder.InitialCatalog, "Wayhunter_M5_IntegrationTest", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
            throw new InvalidOperationException("Only the isolated M5 LocalDB is allowed.");
        _connectionString = builder.ConnectionString;
    }

    private SqlConnection CreateConnection() => new(_connectionString);
}
