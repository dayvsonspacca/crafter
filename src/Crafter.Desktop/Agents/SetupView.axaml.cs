using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Crafter.Presentation.Agents;

namespace Crafter.Desktop.Agents;

public sealed partial class SetupView : UserControl
{
    private SetupViewModel? viewModel;

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

        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= Follow;
        }

        viewModel = DataContext as SetupViewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += Follow;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Settle();
    }

    private void Follow(object? sender, PropertyChangedEventArgs changed)
    {
        if (changed.PropertyName == nameof(SetupViewModel.IsPicking))
        {
            Settle();
        }
    }

    private void Settle() => Dispatcher.UIThread.Post(
        () =>
        {
            if (IsEffectivelyVisible && viewModel is { IsPicking: true })
            {
                (Choices.ContainerFromIndex(0) as ContentPresenter)?.Child?.Focus(NavigationMethod.Tab);
            }
        },
        DispatcherPriority.Loaded);

    private void Press(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && viewModel is { IsPicking: true })
        {
            e.Handled = true;
            if (viewModel.ContinueCommand.CanExecute(null))
            {
                viewModel.ContinueCommand.Execute(null);
            }
        }
    }
}
