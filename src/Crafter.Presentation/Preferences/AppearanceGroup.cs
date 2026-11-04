using Crafter.Core.Preferences;
using Crafter.Presentation.Themes;

namespace Crafter.Presentation.Preferences;

public sealed class AppearanceGroup : PreferenceGroup
{
    private readonly PreferenceBook preferences;

    private readonly IThemeSwitcher themes;

    public AppearanceGroup(PreferenceBook preferences, IThemeSwitcher themes)
        : base("Appearance")
    {
        this.preferences = preferences;
        this.themes = themes;

        var current = Theme.Named(preferences.Current.Theme);
        Themes = [.. Theme.All.Select(theme => new ThemeChoice(theme, theme == current, Choose))];
        themes.Apply(current);
    }

    public IReadOnlyList<ThemeChoice> Themes { get; }

    private void Choose(ThemeChoice chosen)
    {
        foreach (var choice in Themes)
        {
            choice.IsChosen = choice == chosen;
        }

        themes.Apply(chosen.Theme);
        preferences.ChooseTheme(chosen.Theme.Id);
    }
}
