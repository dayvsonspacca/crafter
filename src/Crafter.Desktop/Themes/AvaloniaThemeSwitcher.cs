using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Crafter.Presentation.Themes;

namespace Crafter.Desktop.Themes;

public sealed class AvaloniaThemeSwitcher : IThemeSwitcher
{
    // On Linux the system's appearance comes over DBus a moment after start, and until then
    // Avalonia reports light. It only says so when the answer differs from that default, so
    // an assumed appearance stands until the answer or, when none comes, this long.
    private static readonly TimeSpan Settling = TimeSpan.FromSeconds(2);

    private Theme theme = Theme.Redstone;

    private Appearance? assumed;

    public AvaloniaThemeSwitcher()
    {
        if (Settings is { } settings)
        {
            settings.ColorValuesChanged += Follow;
        }
    }

    public event EventHandler? AppearanceChanged;

    public Appearance Appearance => assumed ?? Reported;

    private static Appearance Reported =>
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

    public void Assume(Appearance appearance)
    {
        assumed = appearance;
        DispatcherTimer.RunOnce(Settle, Settling);
    }

    // No answer by now means the system reports what Avalonia already holds.
    internal void Settle()
    {
        if (assumed is not { } was)
        {
            return;
        }

        assumed = null;
        if (Reported != was)
        {
            Repaint();
        }
    }

    private void Follow(object? sender, PlatformColorValues values)
    {
        assumed = null;
        Repaint();
    }

    private void Repaint()
    {
        Apply(theme);
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
