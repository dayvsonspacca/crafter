namespace Crafter.Presentation.Themes;

public interface IThemeSwitcher
{
    Appearance Appearance { get; }

    event EventHandler? AppearanceChanged;

    void Apply(Theme theme);
}
