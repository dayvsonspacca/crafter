using Crafter.Core.Settings;
using Crafter.Desktop.Themes;

namespace Crafter.Desktop.Settings;

public sealed class AppearanceGroup : PreferenceGroup
{
    private readonly PreferenceBook preferences;

    private readonly Action<Theme> apply;

    public AppearanceGroup(PreferenceBook preferences, Action<Theme> apply)
        : base("Appearance")
    {
        this.preferences = preferences;
        this.apply = apply;

        var current = Theme.Named(preferences.Current.Theme);
        Themes = [.. Theme.All.Select(theme => new ThemeChoice(theme, theme == current, Choose))];
        apply(current);
    }

    public IReadOnlyList<ThemeChoice> Themes { get; }

    private void Choose(ThemeChoice chosen)
    {
        foreach (var choice in Themes)
        {
            choice.IsChosen = choice == chosen;
        }

        apply(chosen.Theme);
        preferences.ChooseTheme(chosen.Theme.Id);
    }
}
