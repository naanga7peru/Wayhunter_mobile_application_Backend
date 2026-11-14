
namespace Wayhunter.Application.Modules.IdentitySocial.Navigation;

public enum WayhunterModule
{
    Traveler = 1,
    Hunter = 2,
    Game = 3
}

public sealed record BottomNavigationItem(
    string TabKey,
    string? DestinationKey);

public static class NavigationContract
{
    // Second Brain design reference
    public const string DecisionReference = "D01";

    // Common application destinations
    public const string Home = "home";
    public const string Profile = "profile";
    public const string Friends = "friends";
    public const string Wallet = "wallet";

    // Authentication destinations
    public const string Login = "auth.login";
    public const string Register = "auth.register";
    public const string OtpVerification = "auth.otp";
    public const string ForgotPassword = "auth.forgot-password";
    public const string ResetPassword = "auth.reset-password";

    // Module destinations
    public const string ModuleSelection = "module.selection";

    public const string TravelerHome = "traveler.home";
    public const string HunterHome = "hunter.home";
    public const string GameHome = "game.home";

    // Leaderboards belong to individual modules
    public const string TravelerLeaderboard =
        "traveler.leaderboard";

    public const string HunterLeaderboard =
        "hunter.leaderboard";

    public const string GameLeaderboard =
        "game.leaderboard";

    public static IReadOnlyList<BottomNavigationItem>
        BuildBottomNavigation(WayhunterModule? selectedModule)
    {
        string moduleDestination = selectedModule switch
        {
            WayhunterModule.Traveler => TravelerHome,
            WayhunterModule.Hunter => HunterHome,
            WayhunterModule.Game => GameHome,
            null => ModuleSelection,
            _ => throw new ArgumentOutOfRangeException(
                nameof(selectedModule))
        };

        string? leaderboardDestination = selectedModule switch
        {
            WayhunterModule.Traveler => TravelerLeaderboard,
            WayhunterModule.Hunter => HunterLeaderboard,
            WayhunterModule.Game => GameLeaderboard,
            null => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(selectedModule))
        };

        return new BottomNavigationItem[]
        {
            new("home", Home),
            new("selected-module", moduleDestination),
            new("leaderboard", leaderboardDestination),
            new("profile", Profile)
        };
    }
}
