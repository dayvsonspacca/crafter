using Crafter.Presentation.Themes;

namespace Crafter.Presentation.Tests.Fakes;

internal sealed class ThemesInMemory : IThemeSwitcher
{
    public event EventHandler? AppearanceChanged;

    public Appearance Appearance { get; private set; } = Appearance.Dark;

    public List<Theme> Applied { get; } = [];

    public void Apply(Theme theme) => Applied.Add(theme);

    public Appearance? Assumed { get; private set; }

    public void Assume(Appearance appearance)
    {
        Assumed = appearance;
        Appearance = appearance;
    }

    public void Turn(Appearance appearance)
    {
        Appearance = appearance;
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
