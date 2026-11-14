
namespace Wayhunter.Domain.Modules.Games.Enums;

public enum GameStatus
{
    PendingApproval,
    Published,
    Rejected,
    Expired,
    Draft,

    // M5-MS03-T05
    // Disabled games must not be playable.
    Disabled
}
