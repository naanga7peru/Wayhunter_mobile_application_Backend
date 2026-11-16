
namespace Wayhunter.Domain.Modules.Games.Enums;

/// <summary>
/// M5-MS07-T01 — Final Game completion review status.
/// Completion does not automatically authorize Coins.
/// </summary>
public enum GameCompletionResultStatus
{
    PendingAdmin = 0,
    Approved = 1,
    Rejected = 2
}
