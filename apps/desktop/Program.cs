using Avalonia;

namespace Crafter.Desktop;

public static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CrafterApplication>().UsePlatformDetect();
}
