
namespace Wayhunter.Application.Modules.IdentitySocial.Localization;

public static class SupportedLanguages
{
    public const string English = "en";
    public const string Tamil = "ta";
    public const string Sinhala = "si";

    public static bool TryNormalize(
        string? languageCode,
        out string normalizedCode)
    {
        normalizedCode = string.Empty;

        if (string.IsNullOrWhiteSpace(languageCode))
            return false;

        string code = languageCode.Trim().ToLowerInvariant();

        switch (code)
        {
            case English:
                normalizedCode = English;
                return true;

            case Tamil:
                normalizedCode = Tamil;
                return true;

            case Sinhala:
                normalizedCode = Sinhala;
                return true;

            default:
                return false;
        }
    }

    public static bool IsSupported(string? languageCode)
    {
        return TryNormalize(languageCode, out _);
    }
}
