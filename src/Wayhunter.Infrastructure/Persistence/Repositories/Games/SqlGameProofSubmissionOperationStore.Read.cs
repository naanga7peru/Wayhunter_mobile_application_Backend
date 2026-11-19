using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class SqlGameProofSubmissionOperationStore
{
    public async Task<GameProofSubmissionOperationSnapshot?> GetAsync(
        Guid submissionOperationId,
        CancellationToken cancellationToken = default)
    {
        if (submissionOperationId == Guid.Empty)
            throw new ArgumentException("Operation ID is required.", nameof(submissionOperationId));

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.GameProofSubmissionOperations', N'U') IS NULL
                THROW 51005, 'M5 recovery table is missing. Schema approval required.', 1;

            SELECT
                [SubmissionOperationId], [AuthenticatedPlayerUserId],
                [GameId], [AttemptId], [StepNumber],
                [CaptureSessionId], [MediaProviderNamespace],
                [CanonicalMediaId], [State], [EvidenceId],
                [ClaimToken], [LeaseExpiresAtUtc],
                [ProviderConsumptionConfirmedAtUtc],
                [NextReconciliationAtUtc], [RecoveryAttemptCount],
                [LastErrorCode], [CreatedAtUtc], [UpdatedAtUtc],
                [ProviderReceiptId]
            FROM [dbo].[GameProofSubmissionOperations]
            WHERE [SubmissionOperationId] = @id;
            """;

        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = submissionOperationId;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        Guid? OptionalGuid(int i) =>
            reader.IsDBNull(i) ? null : reader.GetGuid(i);

        string? OptionalString(int i) =>
            reader.IsDBNull(i) ? null : reader.GetString(i);

        DateTimeOffset? OptionalDate(int i) =>
            reader.IsDBNull(i) ? null : reader.GetFieldValue<DateTimeOffset>(i);

        var binding = new GameProofSubmissionOperationBinding(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetInt32(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7));

        return new GameProofSubmissionOperationSnapshot(
            binding,
            (GameProofSubmissionOperationState)reader.GetInt32(8),
            OptionalGuid(9),
            OptionalString(10),
            OptionalDate(11),
            OptionalDate(12),
            OptionalDate(13),
            reader.GetInt32(14),
            OptionalString(15),
            reader.GetFieldValue<DateTimeOffset>(16),
            reader.GetFieldValue<DateTimeOffset>(17),
            OptionalString(18));
    }
}
