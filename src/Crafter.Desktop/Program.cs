using Avalonia;

namespace Crafter.Desktop;

public static class Program
{
    private const string InputMethod = "AVALONIA_IM_MODULE";

    [STAThread]
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable(InputMethod) is null)
        {
            Environment.SetEnvironmentVariable(InputMethod, "none");
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CrafterApplication>().UsePlatformDetect();
}
