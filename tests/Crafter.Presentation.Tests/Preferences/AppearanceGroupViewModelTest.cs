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

    private static AppearanceGroupViewModel Group(ThemesInMemory themes) =>
        new(new PreferenceBook(new PreferencesInMemory(UserPreferences.Default("/home/hero"))), themes);
}
