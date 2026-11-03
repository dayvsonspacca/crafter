using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Crafter.Desktop.Settings;

public sealed partial class PreferencesView : UserControl
{
    public PreferencesView() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Dispatcher.UIThread.Post(() => Focus());
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Leave(this, e);
        }
    }

    private void Leave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesScreen preferences)
        {
            preferences.LeaveCommand.Execute(null);
            e.Handled = true;
        }
    }
}
