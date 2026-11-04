using Crafter.Core.Preferences;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Presentation.Themes;
using Crafter.Testing;

namespace Crafter.Presentation.Tests.Preferences;

public sealed class AppearanceGroupViewModelTest
{
    [Fact]
    public void PreviewsEachThemeInTheSystemAppearance()
    {
        var themes = new ThemesInMemory();
        themes.Turn(Appearance.Light);

        var group = Group(themes);

        Assert.All(group.Themes, choice => Assert.Equal(choice.Theme.Light, choice.Palette));
    }

    [Fact]
    public void RepaintsThePreviewsWhenTheSystemTurnsDark()
    {
        var themes = new ThemesInMemory();
        themes.Turn(Appearance.Light);
        var group = Group(themes);

        themes.Turn(Appearance.Dark);

        Assert.All(group.Themes, choice => Assert.Equal(choice.Theme.Dark, choice.Palette));
    }

    [Fact]
    public void AppliesTheChosenTheme()
    {
        var themes = new ThemesInMemory();
        var group = Group(themes);

        group.Themes.Single(choice => choice.Theme == Theme.Warped).ChooseCommand.Execute(null);

        Assert.Equal(Theme.Warped, themes.Applied[^1]);
    }

    [Fact]
    public void PaintsWithTheAppearanceTheSystemHadLastTimeUntilItAnswers()
    {
        var themes = new ThemesInMemory();
        themes.Turn(Appearance.Light);

        Group(themes, new UserPreferences("/home/hero") { Theme = "copper", SystemDark = true });

        Assert.Equal(Appearance.Dark, themes.Assumed);
        Assert.Equal(Theme.Copper, themes.Applied[^1]);
        Assert.Equal(Appearance.Dark, themes.Appearance);
    }

    [Fact]
    public void AssumesNothingTheFirstTime()
    {
        var themes = new ThemesInMemory();

        Group(themes);

        Assert.Null(themes.Assumed);
    }

    [Fact]
    public void RemembersTheAppearanceTheSystemReports()
    {
        var themes = new ThemesInMemory();
        var store = new PreferencesInMemory(UserPreferences.Default("/home/hero"));
        _ = new AppearanceGroupViewModel(new PreferenceBook(store), themes);

        themes.Turn(Appearance.Dark);
        Assert.True(store.Saved.SystemDark);

        themes.Turn(Appearance.Light);
        Assert.False(store.Saved.SystemDark);
    }

    private static AppearanceGroupViewModel Group(ThemesInMemory themes, UserPreferences? stored = null) =>
        new(new PreferenceBook(new PreferencesInMemory(stored ?? UserPreferences.Default("/home/hero"))), themes);
}
