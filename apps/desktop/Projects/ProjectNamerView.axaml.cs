using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Crafter.Desktop.Projects;

public sealed partial class ProjectNamerView : UserControl
{
    public ProjectNamerView()
    {
        InitializeComponent();
        NameBox.AddHandler(KeyDownEvent, Steer, RoutingStrategies.Tunnel);
    }

    private ProjectNamer? Namer => DataContext as ProjectNamer;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Dispatcher.UIThread.Post(() =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            });
        }
    }

    private void Steer(object? sender, KeyEventArgs e)
    {
        if (Namer is not { } namer)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter when namer.FinishCommand.CanExecute(null):
                namer.Finish();
                break;
            case Key.Escape:
                namer.Back();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Dismiss(object? sender, RoutedEventArgs e) => Namer?.Dismiss();
}
