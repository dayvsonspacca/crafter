using Avalonia;

namespace Crafter.Desktop;

public static class Program
{
    private const string InputMethod = "AVALONIA_IM_MODULE";

    private const string NativeWayland = "CRAFTER_WAYLAND";

    [STAThread]
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable(InputMethod) is null)
        {
            Environment.SetEnvironmentVariable(InputMethod, "none");
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia's native Wayland backend is still experimental, so it is opt-in. Through XWayland,
    // Hyprland hands drops from Wayland apps over wrong (the clipboard instead of the file);
    // talking to the compositor directly takes them as they are.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<CrafterApplication>().UsePlatformDetect();

        if (OperatingSystem.IsLinux()
            && Environment.GetEnvironmentVariable(NativeWayland) == "1"
            && Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null)
        {
            builder = builder.UseWayland();
        }

        return builder;
    }
}
