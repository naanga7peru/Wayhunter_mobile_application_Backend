namespace Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;

/// <summary>
/// SQL Server command proposals for M7 recovery fencing.
///
/// NOT EXECUTED, NOT REGISTERED, NOT APPROVED.
/// Final schema and real-SQL concurrency verification are required.
///
/// Parameters must be bound using typed SQL parameters.
/// Never concatenate user input into these statements.
/// </summary>
public static class AdvertisementImageRecoverySqlFencingDraft
{
    /// <summary>
    /// Atomically claim an unknown-outcome operation only when
    /// unclaimed or expired and due for verification.
    ///
    /// Parameters:
    /// @UploadOperationId uniqueidentifier
    /// @ClaimToken uniqueidentifier (new nonempty token)
    /// @LeaseSeconds int (positive, bounded)
    ///
    /// Exactly one OUTPUT row means a claim was acquired.
    /// No OUTPUT row means claim rejected.
    /// A lost SQL response must be treated as uncertain.
    /// </summary>
    public const string ClaimUnknownOutcome = """
        UPDATE [dbo].[AdvertisementImageUploadOperations]
        SET
            [ClaimToken] = @ClaimToken,
            [LeaseExpiresAtUtc] =
                TODATETIMEOFFSET(
                    DATEADD(SECOND, @LeaseSeconds, SYSUTCDATETIME()),
                    '+00:00'),
            [Revision] = [Revision] + 1,
            [UpdatedAtUtc] =
                TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
        OUTPUT
            INSERTED.[UploadOperationId],
            INSERTED.[Revision],
            INSERTED.[ClaimToken],
            INSERTED.[LeaseExpiresAtUtc]
        WHERE
            [UploadOperationId] = @UploadOperationId
            AND @ClaimToken <> '00000000-0000-0000-0000-000000000000'
            AND @LeaseSeconds BETWEEN 30 AND 300
            AND [State] IN (1, 3)
            AND [Revision] < 9223372036854775807
            AND (
                [NextRetryAtUtc] IS NULL
                OR [NextRetryAtUtc] <=
                    TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
            )
            AND (
                (
                    [ClaimToken] IS NULL
                    AND [LeaseExpiresAtUtc] IS NULL
                )
                OR [LeaseExpiresAtUtc] <=
                    TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
            );
        """;

    /// <summary>
    /// Narrow recovery-only save command:
    /// schedule retry while retaining unknown state, or
    /// escalate to ManualReview = 8.
    ///
    /// NOT a complete implementation of TrySaveAsync.
    /// Does not commit uploads or authorize cleanup.
    ///
    /// Parameters:
    /// @UploadOperationId uniqueidentifier
    /// @ClaimToken uniqueidentifier
    /// @ExpectedRevision bigint
    /// @NextState int
    /// @NextRetryCount int
    /// @NextRetryAtUtc datetimeoffset nullable
    /// @ErrorCode nvarchar(128) nullable
    ///
    /// Zero affected rows means a failed fence or predicate.
    /// SQL transport failures mean the outcome is unknown.
    /// </summary>
    public const string SaveVerificationOutcome = """
        UPDATE [dbo].[AdvertisementImageUploadOperations]
        SET
            [State] = @NextState,
            [RetryCount] = @NextRetryCount,
            [NextRetryAtUtc] = @NextRetryAtUtc,
            [LastErrorCode] = @ErrorCode,
            [Revision] = [Revision] + 1,
            [UpdatedAtUtc] =
                TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
        OUTPUT
            INSERTED.[UploadOperationId],
            INSERTED.[Revision]
        WHERE
            [UploadOperationId] = @UploadOperationId
            AND [ClaimToken] = @ClaimToken
            AND [LeaseExpiresAtUtc] >
                TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
            AND [Revision] = @ExpectedRevision
            AND [State] IN (1, 3)
            AND [Revision] < 9223372036854775807
            AND @NextRetryCount >= [RetryCount]
            AND @NextRetryCount >= 0
            AND (
                (
                    @NextState = [State]
                    AND @NextRetryAtUtc IS NOT NULL
                )
                OR (
                    @NextState = 8
                    AND @NextRetryAtUtc IS NULL
                )
            );
        """;
}