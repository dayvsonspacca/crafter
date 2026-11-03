using Crafter.Core.Settings;
using Crafter.Desktop.Themes;

namespace Crafter.Desktop.Settings;

public sealed class AppearanceGroup : PreferenceGroup
{
    private readonly IPreferencesStore store;

    private readonly Action<Theme> apply;

    public AppearanceGroup(IPreferencesStore store, Action<Theme> apply)
        : base("Appearance")
    {
        this.store = store;
        this.apply = apply;

        var current = Theme.Named(store.Load().Theme);
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
        store.Save(store.Load() with { Theme = chosen.Theme.Id });
    }
}
