
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class GameModerationService
{
    private readonly IGameModerationStore _store;

    public GameModerationService(
        IGameModerationStore store)
    {
        _store = store ??
            throw new ArgumentNullException(nameof(store));
    }

    // =====================================
    // ADMIN REJECT / FLAG / DISABLE
    // M5-MS03-T05
    // =====================================

    public async Task<GameModerationAuditRecord>
        ModerateAsync(
            GameModerationCommand command,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        if (command.DecisionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Decision ID is required.");
        }

        if (command.GameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.");
        }

        if (string.IsNullOrWhiteSpace(
            command.AdminUserId))
        {
            throw new ArgumentException(
                "Admin User ID is required.");
        }

        if (!Enum.IsDefined(command.Action))
        {
            throw new ArgumentException(
                "Unsupported moderation action.");
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new ArgumentException(
                "Moderation reason is required.");
        }

        string cleanReason = command.Reason.Trim();

        if (cleanReason.Length > 1000)
        {
            throw new ArgumentException(
                "Moderation reason exceeds 1000 characters.");
        }

        if (command.OccurredAtUtc == default ||
            command.OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC timestamp is required.");
        }

        if (command.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.");
        }

        // Normalize input values.
        var validatedCommand = command with
        {
            AdminUserId = command.AdminUserId.Trim(),
            Reason = cleanReason
        };

        // Actual Game state validation, trusted
        // Admin identity, and atomic audit storage
        // belong to the implementation boundary.
        return await _store.ApplyAsync(
            validatedCommand,
            cancellationToken);
    }
}
