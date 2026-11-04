using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Crafter.Presentation.Folders;

namespace Crafter.Desktop.Folders;

public sealed partial class FolderPickerView : UserControl
{
    public FolderPickerView()
    {
        InitializeComponent();
        FilterBox.AddHandler(KeyDownEvent, Steer, RoutingStrategies.Tunnel);
    }

    private FolderPickerViewModel? Picker => DataContext as FolderPickerViewModel;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Dispatcher.UIThread.Post(() => FilterBox.Focus());
        }
    }

    private void Steer(object? sender, KeyEventArgs e)
    {
        if (Picker is not { } picker)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                picker.Move(1);
                break;
            case Key.Up:
                picker.Move(-1);
                break;
            case Key.Tab:
                picker.Enter();
                break;
            case Key.Back when picker.Filter.Length == 0:
                picker.Up();
                break;
            case Key.Enter:
                picker.Choose();
                break;
            case Key.Escape:
                picker.Cancel();
                break;
            default:
                return;
        }

        e.Handled = true;
        FilterBox.CaretIndex = picker.Filter.Length;
    }

    private void Chosen(object? sender, TappedEventArgs e) => Picker?.Choose();

    private void Dismiss(object? sender, RoutedEventArgs e) => Picker?.Cancel();
}
