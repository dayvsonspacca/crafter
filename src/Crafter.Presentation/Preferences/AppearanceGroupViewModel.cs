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

        // The system answers a moment after start; until then the appearance it had last time
        // keeps a dark desktop from flashing the light variant.
        if (preferences.Current.SystemDark is { } dark)
        {
            themes.Assume(dark ? Appearance.Dark : Appearance.Light);
        }

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
        preferences.RememberSystem(themes.Appearance == Appearance.Dark);

        foreach (var choice in Themes)
        {
            choice.Appearance = themes.Appearance;
        }
    }
}
