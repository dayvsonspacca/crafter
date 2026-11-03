using Avalonia;
using Crafter.Presentation.Themes;

namespace Crafter.Desktop.Themes;

public sealed class ApplicationThemeSwitcher : IThemeSwitcher
{
    public void Apply(Theme theme)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = ThemeVariants.For(theme);
        }
    }
}
