using Avalonia;
using Avalonia.Platform;
using Crafter.Presentation.Themes;

namespace Crafter.Desktop.Themes;

public sealed class AvaloniaThemeSwitcher : IThemeSwitcher
{
    private Theme theme = Theme.Redstone;

    public AvaloniaThemeSwitcher()
    {
        if (Settings is { } settings)
        {
            settings.ColorValuesChanged += Follow;
        }
    }

    public event EventHandler? AppearanceChanged;

    public Appearance Appearance =>
        Settings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Light ? Appearance.Light : Appearance.Dark;

    private static IPlatformSettings? Settings => Application.Current?.PlatformSettings;

    public void Apply(Theme theme)
    {
        this.theme = theme;

        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = ThemeVariants.For(theme, Appearance);
        }
    }

    private void Follow(object? sender, PlatformColorValues values)
    {
        Apply(theme);
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
