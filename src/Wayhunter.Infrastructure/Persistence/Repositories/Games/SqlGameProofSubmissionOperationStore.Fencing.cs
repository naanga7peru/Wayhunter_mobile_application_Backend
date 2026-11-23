using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class SqlGameProofSubmissionOperationStore
{
    public async Task<bool> TryMarkProviderOutcomeUnknownAsync(
        GameProofSubmissionOperationBinding binding,
        string expectedClaimToken,
        CancellationToken cancellationToken = default)
    {
        GameProofSubmissionOperationRules.Validate(binding);

        if (string.IsNullOrWhiteSpace(expectedClaimToken) || expectedClaimToken.Length > 128)
            throw new ArgumentException("Invalid expected claim token.", nameof(expectedClaimToken));

        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = (Microsoft.Data.SqlClient.SqlTransaction)transaction;

        command.CommandText = """
            IF OBJECT_ID(N'dbo.GameProofSubmissionOperations', N'U') IS NULL
                THROW 51005, 'M5 recovery table missing. Schema approval required.', 1;

            SELECT
                [SubmissionOperationId], [AuthenticatedPlayerUserId],
                [GameId], [AttemptId], [StepNumber],
                [CaptureSessionId], [MediaProviderNamespace],
                [CanonicalMediaId], [State],
                [ClaimToken], [LeaseExpiresAtUtc]
            FROM [dbo].[GameProofSubmissionOperations] WITH (UPDLOCK, HOLDLOCK)
            WHERE [SubmissionOperationId] = @id;
            """;

        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = binding.SubmissionOperationId;

        await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return false;

            var storedBinding = new GameProofSubmissionOperationBinding(
                reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2),
                reader.GetGuid(3), reader.GetInt32(4), reader.GetString(5),
                reader.GetString(6), reader.GetString(7));

            GameProofSubmissionOperationRules.EnsureMatchingBinding(storedBinding, binding);

            if ((GameProofSubmissionOperationState)reader.GetInt32(8)
                != GameProofSubmissionOperationState.VerificationInFlight)
                return false;

            if (reader.IsDBNull(9) ||
                !string.Equals(reader.GetString(9), expectedClaimToken, StringComparison.Ordinal))
                return false;

            if (reader.IsDBNull(10))
                return false;
        }

        command.CommandText = """
            DECLARE @serverNow datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

            UPDATE [dbo].[GameProofSubmissionOperations]
            SET [State] = 2,
                [ClaimToken] = NULL,
                [LeaseExpiresAtUtc] = @serverNow,
                [NextReconciliationAtUtc] = @serverNow,
                [UpdatedAtUtc] = @serverNow
            WHERE [SubmissionOperationId] = @id
              AND [State] = 1
              AND [ClaimToken] COLLATE Latin1_General_100_BIN2 = @claim
              AND [LeaseExpiresAtUtc] > @serverNow;
            """;

        command.Parameters.Add("@claim", SqlDbType.NVarChar, 128).Value = expectedClaimToken;

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
            return false;

        if (affectedRows != 1)
            throw new InvalidOperationException("Unexpected fencing update count.");

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
