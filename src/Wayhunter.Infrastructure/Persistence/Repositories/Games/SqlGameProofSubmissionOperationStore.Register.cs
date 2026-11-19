using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class SqlGameProofSubmissionOperationStore
{
    public async Task<GameProofSubmissionOperationSnapshot> RegisterOrReadAsync(
        GameProofSubmissionOperationBinding binding,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        GameProofSubmissionOperationRules.Validate(binding);

        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Valid server UTC time is required.", nameof(nowUtc));

        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = (Microsoft.Data.SqlClient.SqlTransaction)transaction;
        command.CommandText = """
            DECLARE @serverNow datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

            IF OBJECT_ID(N'dbo.GameProofSubmissionOperations', N'U') IS NULL
                THROW 51005, 'M5 recovery table missing. Schema approval required.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM [dbo].[GameProofSubmissionOperations] WITH (UPDLOCK, HOLDLOCK)
                WHERE [SubmissionOperationId] = @id
            )
            BEGIN
                INSERT INTO [dbo].[GameProofSubmissionOperations]
                (
                    [SubmissionOperationId], [AuthenticatedPlayerUserId],
                    [GameId], [AttemptId], [StepNumber],
                    [CaptureSessionId], [MediaProviderNamespace],
                    [CanonicalMediaId], [State],
                    [CreatedAtUtc], [UpdatedAtUtc], [RecoveryAttemptCount]
                )
                VALUES
                (
                    @id, @player, @game, @attempt, @step,
                    @capture, @provider, @media, 0,
                    @serverNow, @serverNow, 0
                );
            END;
            """;

        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = binding.SubmissionOperationId;
        command.Parameters.Add("@player", SqlDbType.NVarChar, 256).Value = binding.AuthenticatedPlayerUserId;
        command.Parameters.Add("@game", SqlDbType.UniqueIdentifier).Value = binding.GameId;
        command.Parameters.Add("@attempt", SqlDbType.UniqueIdentifier).Value = binding.AttemptId;
        command.Parameters.Add("@step", SqlDbType.Int).Value = binding.StepNumber;
        command.Parameters.Add("@capture", SqlDbType.NVarChar, 256).Value = binding.CaptureSessionId;
        command.Parameters.Add("@provider", SqlDbType.NVarChar, 64).Value = binding.MediaProviderNamespace;
        command.Parameters.Add("@media", SqlDbType.NVarChar, 256).Value = binding.CanonicalMediaId;


        await command.ExecuteNonQueryAsync(cancellationToken);

        // Verify immutable binding on the same connection and transaction.
        command.CommandText = "SELECT [SubmissionOperationId], [AuthenticatedPlayerUserId], [GameId], [AttemptId], [StepNumber], [CaptureSessionId], [MediaProviderNamespace], [CanonicalMediaId] FROM [dbo].[GameProofSubmissionOperations] WITH (UPDLOCK, HOLDLOCK) WHERE [SubmissionOperationId] = @id;";
        await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Operation missing inside registration transaction.");
            var storedBinding = new GameProofSubmissionOperationBinding(
                reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2),
                reader.GetGuid(3), reader.GetInt32(4), reader.GetString(5),
                reader.GetString(6), reader.GetString(7));
            GameProofSubmissionOperationRules.EnsureMatchingBinding(storedBinding, binding);
        }
        try
        {
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception commitError) when (!cancellationToken.IsCancellationRequested)
        {
            // A failed COMMIT acknowledgement does not prove rollback.
            var cleanupErrors = new System.Collections.Generic.List<Exception>();
            try { await transaction.DisposeAsync(); } catch (Exception ex) { cleanupErrors.Add(ex); }
            try { await connection.DisposeAsync(); } catch (Exception ex) { cleanupErrors.Add(ex); }
            if (cleanupErrors.Count > 0)
            {
                cleanupErrors.Insert(0, commitError);
                throw new AggregateException("Registration commit outcome unknown; cleanup failed.", cleanupErrors);
            }
            GameProofSubmissionOperationSnapshot? recovered;
            try
            {
                recovered = await GetAsync(binding.SubmissionOperationId, cancellationToken);
                if (recovered is not null)
                    GameProofSubmissionOperationRules.EnsureMatchingStoredOperation(recovered, binding);
            }
            catch (Exception recoveryError)
            {
                throw new AggregateException("Registration commit outcome unknown; recovery verification failed.", commitError, recoveryError);
            }
            if (recovered is null)
                throw;
            return recovered;
        }

        var stored = await GetAsync(
            binding.SubmissionOperationId, cancellationToken)
            ?? throw new InvalidOperationException("Registered operation could not be read.");

        GameProofSubmissionOperationRules.EnsureMatchingStoredOperation(
            stored, binding);

        return stored;
    }
}
