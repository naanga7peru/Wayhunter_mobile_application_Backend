
namespace Wayhunter.Application.Modules.IdentitySocial.Localization;

public static class LocalizationKeys
{
    // Bottom navigation
    public const string NavHome = "nav.home";
    public const string NavModule = "nav.module";
    public const string NavLeaderboard = "nav.leaderboard";
    public const string NavProfile = "nav.profile";

    // Main modules
    public const string Traveler = "module.traveler";
    public const string Hunter = "module.hunter";
    public const string Game = "module.game";

    // Authentication
    public const string Login = "auth.login";
    public const string Register = "auth.register";
    public const string VerifyOtp = "auth.verify_otp";
    public const string ForgotPassword = "auth.forgot_password";
    public const string ResetPassword = "auth.reset_password";

    // Profile and social
    public const string ProfileTitle = "profile.title";
    public const string EditProfile = "profile.edit";
    public const string Friends = "friends.title";
    public const string Followers = "friends.followers";
    public const string Following = "friends.following";

    // Wallet
    public const string Wallet = "wallet.title";
    public const string Coins = "wallet.coins";

    // General
    public const string Save = "common.save";
    public const string Cancel = "common.cancel";
    public const string Logout = "common.logout";

    public static IReadOnlyList<string> RequiredKeys { get; } =
        Array.AsReadOnly(new[]
        {
            NavHome,
            NavModule,
            NavLeaderboard,
            NavProfile,
            Traveler,
            Hunter,
            Game,
            Login,
            Register,
            VerifyOtp,
            ForgotPassword,
            ResetPassword,
            ProfileTitle,
            EditProfile,
            Friends,
            Followers,
            Following,
            Wallet,
            Coins,
            Save,
            Cancel,
            Logout
        });
}
