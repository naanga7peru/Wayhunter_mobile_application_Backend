
using Wayhunter.Application.Modules.IdentitySocial.Localization;
using Wayhunter.Application.Modules.IdentitySocial.Navigation;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class NavigationLanguageTests
{
    [Fact]
    public void BottomNavigation_ShouldHaveFourTabs()
    {
        var tabs = NavigationContract.BuildBottomNavigation(
            WayhunterModule.Traveler);

        Assert.Equal(4, tabs.Count);

        Assert.Equal("home", tabs[0].TabKey);
        Assert.Equal("selected-module", tabs[1].TabKey);
        Assert.Equal("leaderboard", tabs[2].TabKey);
        Assert.Equal("profile", tabs[3].TabKey);
    }

    [Theory]
    [InlineData(
        WayhunterModule.Traveler,
        "traveler.home",
        "traveler.leaderboard")]
    [InlineData(
        WayhunterModule.Hunter,
        "hunter.home",
        "hunter.leaderboard")]
    [InlineData(
        WayhunterModule.Game,
        "game.home",
        "game.leaderboard")]
    public void SelectedModule_ShouldControlDestinations(
        WayhunterModule module,
        string expectedHome,
        string expectedLeaderboard)
    {
        var tabs = NavigationContract.BuildBottomNavigation(module);

        Assert.Equal(expectedHome, tabs[1].DestinationKey);
        Assert.Equal(expectedLeaderboard, tabs[2].DestinationKey);
    }

    [Fact]
    public void NoSelectedModule_ShouldRequireModuleSelection()
    {
        var tabs = NavigationContract.BuildBottomNavigation(null);

        Assert.Equal(
            NavigationContract.ModuleSelection,
            tabs[1].DestinationKey);

        Assert.Null(tabs[2].DestinationKey);
    }

    [Fact]
    public void Navigation_ShouldNotUseGlobalLeaderboard()
    {
        foreach (var module in Enum.GetValues<WayhunterModule>())
        {
            var tabs = NavigationContract.BuildBottomNavigation(module);

            Assert.DoesNotContain(
                tabs,
                tab => tab.DestinationKey == "leaderboard.global");
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ta")]
    [InlineData("si")]
    [InlineData("TA")]
    [InlineData(" si ")]
    public void SupportedLanguage_ShouldBeAccepted(string code)
    {
        Assert.True(SupportedLanguages.IsSupported(code));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData(null)]
    public void UnsupportedLanguage_ShouldBeRejected(string? code)
    {
        Assert.False(SupportedLanguages.IsSupported(code));
    }

    [Fact]
    public void TamilLanguage_ShouldNormalizeCorrectly()
    {
        var result = SupportedLanguages.TryNormalize(
            " TA ",
            out var normalized);

        Assert.True(result);
        Assert.Equal("ta", normalized);
    }

    [Fact]
    public void LocalizationKeys_ShouldBeUnique()
    {
        var keys = LocalizationKeys.RequiredKeys;

        Assert.Equal(
            keys.Count,
            keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void LocalizationKeys_ShouldNotBeEmpty()
    {
        Assert.All(
            LocalizationKeys.RequiredKeys,
            key => Assert.False(string.IsNullOrWhiteSpace(key)));
    }

    [Fact]
    public void NavigationContract_ShouldReferenceD01()
    {
        Assert.Equal("D01", NavigationContract.DecisionReference);
    }
}
