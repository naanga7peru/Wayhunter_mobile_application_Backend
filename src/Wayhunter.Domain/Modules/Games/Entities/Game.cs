
using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class Game
{
    public Guid Id { get; }
    public string CreatorUserId { get; }

    public GameStatus Status { get; private set; }
        = GameStatus.PendingApproval;

    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public string? WinnerUserId { get; private set; }

    public Game(Guid id, string creatorUserId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Game ID required.");

        if (string.IsNullOrWhiteSpace(creatorUserId))
            throw new ArgumentException("Creator ID required.");

        Id = id;
        CreatorUserId = creatorUserId;
    }

    public void ApproveCreation(DateTimeOffset approvedAtUtc)
    {
        if (Status != GameStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending games can be approved.");

        RequireUtc(approvedAtUtc);

        ApprovedAtUtc = approvedAtUtc;
        ExpiresAtUtc = approvedAtUtc.AddHours(30 * 24);
        Status = GameStatus.Published;
    }

    public void RejectCreation()
    {
        if (Status != GameStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending games can be rejected.");

        Status = GameStatus.Rejected;
    }

    public bool CanPlay(DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);

        return Status == GameStatus.Published
            && WinnerUserId == null
            && ExpiresAtUtc.HasValue
            && nowUtc < ExpiresAtUtc.Value;
    }

    // Call only after Admin verifies the winning attempt.
    public void ApproveVerifiedWinner(
        string playerUserId,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(playerUserId))
            throw new ArgumentException("Player ID required.");

        if (playerUserId == CreatorUserId)
            throw new InvalidOperationException(
                "Creator cannot win their own game.");

        if (!CanPlay(nowUtc))
            throw new InvalidOperationException(
                "Game expired, closed or already won.");

        WinnerUserId = playerUserId;
    }

    public bool ArchiveIfExpired(DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);

        if (Status != GameStatus.Published ||
            !ExpiresAtUtc.HasValue ||
            nowUtc < ExpiresAtUtc.Value)
            return false;

        Status = GameStatus.Expired;
        return true;
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("UTC timestamp required.");
    }
}
