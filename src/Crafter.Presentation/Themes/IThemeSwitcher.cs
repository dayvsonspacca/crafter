namespace Crafter.Presentation.Themes;

public interface IThemeSwitcher
{
    Appearance Appearance { get; }

    event EventHandler? AppearanceChanged;

    void Apply(Theme theme);

    // Paint with this appearance until the system reports its own, which can take a moment.
    void Assume(Appearance appearance);
}
