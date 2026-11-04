using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop.Controls;

public sealed partial class TitleBar : UserControl
{
    private WindowManager windowManager = new StackingWindowManager();

    public TitleBar() => InitializeComponent();

    private Window Host => TopLevel.GetTopLevel(this) as Window
        ?? throw new InvalidOperationException("A title bar only lives inside a window.");

    public void ArrangeFor(WindowManager windowManager)
    {
        this.windowManager = windowManager;

        MinimizeButton.IsVisible = windowManager.Minimizes;
        MaximizeButton.IsVisible = windowManager.Maximizes;
    }

    public void Follow(WindowState state)
    {
        var maximized = state == WindowState.Maximized;

        MaximizeIcon.IsVisible = !maximized;
        RestoreIcon.IsVisible = maximized;
        AutomationProperties.SetName(MaximizeButton, maximized ? "Restore" : "Maximize");
    }

    private void Drag(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            if (windowManager.Maximizes)
            {
                ToggleMaximized();
            }

            return;
        }

        Host.BeginMoveDrag(e);
    }

    private void Minimize(object? sender, RoutedEventArgs e) => Host.WindowState = WindowState.Minimized;

    private void ToggleMaximize(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void Close(object? sender, RoutedEventArgs e) => Host.Close();

    private void ToggleMaximized() =>
        Host.WindowState = Host.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
