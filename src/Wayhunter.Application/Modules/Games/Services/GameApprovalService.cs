
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class GameApprovalService
{
    private readonly IGameApprovalStore _approvalStore;

    // =====================================
    // CONSTRUCTOR
    // =====================================

    public GameApprovalService(
        IGameApprovalStore approvalStore)
    {
        _approvalStore = approvalStore
            ?? throw new ArgumentNullException(
                nameof(approvalStore));
    }

    // =====================================
    // M5-MS03-T04
    // APPROVE GAME AND ENQUEUE REWARD
    // =====================================

    public async Task<CoinRewardGrantRequest> ApproveAsync(
        GameApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Validate Game ID.
        if (command.GameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(command));
        }

        // 2. Validate Admin identity reference.
        // This does not prove Admin authorization.
        if (string.IsNullOrWhiteSpace(command.AdminUserId))
        {
            throw new ArgumentException(
                "Admin user ID is required.",
                nameof(command));
        }

        // 3. Require an approval record reference.
        if (string.IsNullOrWhiteSpace(
                command.ApprovalRecordId))
        {
            throw new ArgumentException(
                "Approval record ID is required.",
                nameof(command));
        }

        // 4. Require authoritative UTC timestamp.
        if (command.ApprovedAtUtc == default ||
            command.ApprovedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC approval timestamp is required.",
                nameof(command));
        }

        // 5. Validate reward event ID.
        if (command.RewardEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Reward event ID is required.",
                nameof(command));
        }

        // 6. Validate correlation ID.
        if (command.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(command));
        }

        // 7. Validate reward rule version.
        if (string.IsNullOrWhiteSpace(
                command.RewardRuleVersion))
        {
            throw new ArgumentException(
                "Reward rule version is required.",
                nameof(command));
        }

        // 8. Normalize string values.
        var validatedCommand = command with
        {
            AdminUserId =
                command.AdminUserId.Trim(),

            ApprovalRecordId =
                command.ApprovalRecordId.Trim(),

            RewardRuleVersion =
                command.RewardRuleVersion.Trim()
        };

        // 9. Delegate to the persistence boundary.
        //
        // A future store implementation MUST verify:
        // - authenticated Admin authorization
        // - pending Game and approved review record
        // - ownership and eligibility
        // - media validation
        // - Game publication and 30-day expiry
        // - creator reward event generation
        // - atomic transaction and idempotency
        //
        // This service does not credit Coins.
        return await _approvalStore
            .ApproveAndEnqueueCreatorRewardAsync(
                validatedCommand,
                cancellationToken);
    }
}
