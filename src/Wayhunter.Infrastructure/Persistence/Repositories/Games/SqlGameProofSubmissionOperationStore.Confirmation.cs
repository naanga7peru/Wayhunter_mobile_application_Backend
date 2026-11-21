using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class SqlGameProofSubmissionOperationStore
{
    // Isolated M5 store only; caller must authenticate and verify the trusted provider outcome.
    // This method never invokes the provider or authorizes another capture consumption.
    public async Task<bool> TryRecordConfirmedConsumptionAsync(GameProofSubmissionOperationBinding binding, string expectedClaimToken, GameProofCaptureOperationOutcome outcome, CancellationToken cancellationToken = default)
    {
        GameProofCaptureOutcomeRules.Validate(binding, outcome);
        if (!GameProofCaptureOutcomeRules.IsConfirmedConsumed(outcome)) throw new ArgumentException("Confirmed provider consumption with receipt required.", nameof(outcome));
        if (string.IsNullOrWhiteSpace(expectedClaimToken) || expectedClaimToken.Length > 128) throw new ArgumentException("Invalid claim token.", nameof(expectedClaimToken));
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)transaction;
        command.CommandText = "DECLARE @now datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), 0); UPDATE [dbo].[GameProofSubmissionOperations] SET [State] = 3, [ProviderReceiptId] = @receipt, [ProviderConsumptionConfirmedAtUtc] = @now, [ClaimToken] = NULL, [LeaseExpiresAtUtc] = @now, [NextReconciliationAtUtc] = @now, [UpdatedAtUtc] = @now WHERE [SubmissionOperationId] = @id AND [AuthenticatedPlayerUserId] COLLATE Latin1_General_100_BIN2 = @player AND [GameId] = @game AND [AttemptId] = @attempt AND [StepNumber] = @step AND [CaptureSessionId] COLLATE Latin1_General_100_BIN2 = @capture AND [MediaProviderNamespace] COLLATE Latin1_General_100_BIN2 = @provider AND [CanonicalMediaId] COLLATE Latin1_General_100_BIN2 = @media AND [State] IN (1,2) AND [ClaimToken] COLLATE Latin1_General_100_BIN2 = @claim AND [LeaseExpiresAtUtc] > @now AND [ProviderReceiptId] IS NULL;";
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = binding.SubmissionOperationId;
        command.Parameters.Add("@player", SqlDbType.NVarChar, 256).Value = binding.AuthenticatedPlayerUserId;
        command.Parameters.Add("@game", SqlDbType.UniqueIdentifier).Value = binding.GameId;
        command.Parameters.Add("@attempt", SqlDbType.UniqueIdentifier).Value = binding.AttemptId;
        command.Parameters.Add("@step", SqlDbType.Int).Value = binding.StepNumber;
        command.Parameters.Add("@capture", SqlDbType.NVarChar, 256).Value = binding.CaptureSessionId;
        command.Parameters.Add("@provider", SqlDbType.NVarChar, 64).Value = binding.MediaProviderNamespace;
        command.Parameters.Add("@media", SqlDbType.NVarChar, 256).Value = binding.CanonicalMediaId;
        command.Parameters.Add("@claim", SqlDbType.NVarChar, 128).Value = expectedClaimToken;
        command.Parameters.Add("@receipt", SqlDbType.NVarChar, 256).Value = outcome.ProviderReceiptId!;
        var updated = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updated == 0) return false;
        if (updated != 1) throw new InvalidOperationException("Unexpected consumption confirmation update count.");
        try
        {
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception commitError)
        {
            // A failed COMMIT acknowledgement does not prove a rollback.
            try
            {
                await transaction.DisposeAsync();
                await connection.DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Receipt commit outcome unknown; SQL cleanup failed. Do not retry provider consumption.", commitError, cleanupError);
            }
            GameProofSubmissionOperationSnapshot? recovered;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                recovered = await GetAsync(binding.SubmissionOperationId, timeout.Token);
                if (recovered is not null)
                    GameProofSubmissionOperationRules.EnsureMatchingStoredOperation(recovered, binding);
            }
            catch (Exception recoveryError)
            {
                throw new AggregateException("Receipt commit outcome unknown; verification failed. Do not retry provider consumption.", commitError, recoveryError);
            }
            if (recovered is not null && (recovered.State == GameProofSubmissionOperationState.CaptureConsumed || recovered.State == GameProofSubmissionOperationState.Completed) && string.Equals(recovered.ProviderReceiptId, outcome.ProviderReceiptId, StringComparison.Ordinal) && recovered.ProviderConsumptionConfirmedAtUtc is not null)
                return true;
            throw new InvalidOperationException("Receipt commit outcome unconfirmed. Reconcile the operation; do not retry provider consumption.", commitError);
        }
        return true;
    }
}
