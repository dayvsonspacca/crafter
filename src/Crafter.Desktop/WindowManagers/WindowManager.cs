namespace Crafter.Desktop.WindowManagers;

public abstract class WindowManager
{
    private const string NativeWayland = "CRAFTER_WAYLAND";

    public abstract bool Minimizes { get; }

    public abstract bool Maximizes { get; }

    // Whether Crafter should talk Wayland to this compositor directly instead of going through
    // XWayland, Avalonia's default. Only where XWayland misbehaves: the native backend is young.
    public abstract bool PrefersNativeWayland { get; }

    public static WindowManager Detect() => Detect(Environment.GetEnvironmentVariable);

    // On a Wayland session the window manager decides; CRAFTER_WAYLAND=1 or 0 overrides it.
    public static bool UsesNativeWayland() => UsesNativeWayland(Detect(), OperatingSystem.IsLinux(), Environment.GetEnvironmentVariable);

    internal static WindowManager Detect(Func<string, string?> variable) =>
        string.IsNullOrEmpty(variable("HYPRLAND_INSTANCE_SIGNATURE"))
            ? new StackingWindowManager()
            : new HyprlandWindowManager();

    internal static bool UsesNativeWayland(WindowManager manager, bool linux, Func<string, string?> variable) =>
        linux
        && !string.IsNullOrEmpty(variable("WAYLAND_DISPLAY"))
        && variable(NativeWayland) switch
        {
            "1" => true,
            "0" => false,
            _ => manager.PrefersNativeWayland,
        };
}
