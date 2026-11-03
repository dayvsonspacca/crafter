using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Crafter.Desktop.Projects;

public sealed partial class ProjectPaletteView : UserControl
{
    public ProjectPaletteView()
    {
        InitializeComponent();
        QueryBox.AddHandler(KeyDownEvent, Steer, RoutingStrategies.Tunnel);
    }

    private ProjectPalette? Palette => DataContext as ProjectPalette;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Dispatcher.UIThread.Post(() => QueryBox.Focus());
        }
    }

    private void Steer(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Palette?.Move(1);
                break;
            case Key.Up:
                Palette?.Move(-1);
                break;
            case Key.Enter:
                Palette?.Choose();
                break;
            case Key.Escape:
                Palette?.Hide();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Chosen(object? sender, TappedEventArgs e) => Palette?.Choose();

    private void Dismiss(object? sender, PointerPressedEventArgs e) => Palette?.Hide();
}
