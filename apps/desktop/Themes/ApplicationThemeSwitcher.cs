using Avalonia;

namespace Crafter.Desktop.Themes;

public sealed class ApplicationThemeSwitcher : IThemeSwitcher
{
    public void Apply(Theme theme)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = theme.Variant;
        }
    }
}
