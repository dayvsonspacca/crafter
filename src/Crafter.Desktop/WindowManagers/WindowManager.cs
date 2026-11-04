namespace Crafter.Desktop.WindowManagers;

public abstract class WindowManager
{
    public abstract bool Minimizes { get; }

    public abstract bool Maximizes { get; }

    public static WindowManager Detect() =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"))
            ? new StackingWindowManager()
            : new HyprlandWindowManager();
}
