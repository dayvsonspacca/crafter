using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Crafter.Presentation.Agents;

namespace Crafter.Desktop.Setup;

public sealed partial class SetupView : UserControl
{
    private SetupScreen? screen;

    public SetupView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, Press, RoutingStrategies.Tunnel);
        PropertyChanged += (_, changed) =>
        {
            if (changed.Property == IsVisibleProperty)
            {
                Settle();
            }
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (screen is not null)
        {
            screen.PropertyChanged -= Follow;
        }

        screen = DataContext as SetupScreen;
        if (screen is not null)
        {
            screen.PropertyChanged += Follow;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Settle();
    }

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName == nameof(SetupScreen.IsPicking))
        {
            Settle();
        }
    }

    private void Settle() => Dispatcher.UIThread.Post(
        () =>
        {
            if (IsEffectivelyVisible && screen is { IsPicking: true })
            {
                (Choices.ContainerFromIndex(0) as ContentPresenter)?.Child?.Focus(NavigationMethod.Tab);
            }
        },
        DispatcherPriority.Loaded);

    private void Press(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && screen is { IsPicking: true })
        {
            e.Handled = true;
            if (screen.ContinueCommand.CanExecute(null))
            {
                screen.ContinueCommand.Execute(null);
            }
        }
    }
}
