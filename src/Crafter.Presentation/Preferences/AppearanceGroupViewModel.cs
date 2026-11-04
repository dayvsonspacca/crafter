using Crafter.Core.Preferences;
using Crafter.Presentation.Themes;

namespace Crafter.Presentation.Preferences;

public sealed class AppearanceGroupViewModel : PreferenceGroupViewModel
{
    private readonly PreferenceBook preferences;

    private readonly IThemeSwitcher themes;

    public AppearanceGroupViewModel(PreferenceBook preferences, IThemeSwitcher themes)
        : base("Appearance")
    {
        this.preferences = preferences;
        this.themes = themes;

        var current = Theme.Named(preferences.Current.Theme);
        Themes = [.. Theme.All.Select(theme => new ThemeChoiceViewModel(theme, theme == current, themes.Appearance, Choose))];
        themes.AppearanceChanged += Follow;
        themes.Apply(current);
    }

    public IReadOnlyList<ThemeChoiceViewModel> Themes { get; }

    private void Choose(ThemeChoiceViewModel chosen)
    {
        foreach (var choice in Themes)
        {
            choice.IsChosen = choice == chosen;
        }

        themes.Apply(chosen.Theme);
        preferences.ChooseTheme(chosen.Theme.Id);
    }

    private void Follow(object? sender, EventArgs e)
    {
        foreach (var choice in Themes)
        {
            choice.Appearance = themes.Appearance;
        }
    }
}
