using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class SqlGameProofSubmissionOperationStore
{
    public async Task<bool> TryClaimReconciliationAsync(
        GameProofSubmissionOperationBinding binding,
        string recoveryClaimToken,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        GameProofSubmissionOperationRules.Validate(binding);

        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Valid server UTC time is required.", nameof(nowUtc));

        GameProofSubmissionLeaseRules.ValidateClaim(
            recoveryClaimToken, nowUtc, leaseExpiresAtUtc);

        if (leaseExpiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Reconciliation lease expiry must be UTC.", nameof(leaseExpiresAtUtc));

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
                [LeaseExpiresAtUtc], [NextReconciliationAtUtc],
                [ClaimToken]
            FROM [dbo].[GameProofSubmissionOperations] WITH (UPDLOCK, HOLDLOCK)
            WHERE [SubmissionOperationId] = @id;
            """;

        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = binding.SubmissionOperationId;

        GameProofSubmissionOperationState currentState;
        await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return false;

            var storedBinding = new GameProofSubmissionOperationBinding(
                reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2),
                reader.GetGuid(3), reader.GetInt32(4), reader.GetString(5),
                reader.GetString(6), reader.GetString(7));

            GameProofSubmissionOperationRules.EnsureMatchingBinding(storedBinding, binding);
            currentState = (GameProofSubmissionOperationState)reader.GetInt32(8);
        }

        if (!GameProofSubmissionRecoveryRules.RequiresReconciliation(currentState))
            return false;

        command.CommandText = """
            DECLARE @serverNow datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

            UPDATE [dbo].[GameProofSubmissionOperations]
            SET [ClaimToken] = @claim,
                [LeaseExpiresAtUtc] = @lease,
                [NextReconciliationAtUtc] = @lease,
                [UpdatedAtUtc] = @serverNow,
                [RecoveryAttemptCount] = [RecoveryAttemptCount] + 1
            WHERE [SubmissionOperationId] = @id
              AND [State] IN (1, 2, 3)
              AND [LeaseExpiresAtUtc] <= @serverNow
              AND ([NextReconciliationAtUtc] IS NULL OR [NextReconciliationAtUtc] <= @serverNow)
              AND @lease > @serverNow;
            """;

        command.Parameters.Add("@claim", SqlDbType.NVarChar, 128).Value = recoveryClaimToken;
        command.Parameters.Add("@lease", SqlDbType.DateTimeOffset).Value = leaseExpiresAtUtc;

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
            return false;

        if (affectedRows != 1)
            throw new InvalidOperationException("Unexpected recovery claim update count.");

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
