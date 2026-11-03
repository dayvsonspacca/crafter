using Avalonia;
using Avalonia.Controls;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop;

public sealed partial class Workbench : Window
{
    public Workbench()
        : this(WindowManager.Detect())
    {
    }

    public Workbench(WindowManager windowManager)
    {
        InitializeComponent();
        Bar.ArrangeFor(windowManager);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            Bar?.Follow(WindowState);
        }
    }
}
