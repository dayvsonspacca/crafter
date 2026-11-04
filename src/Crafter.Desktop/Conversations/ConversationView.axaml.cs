using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Crafter.Presentation.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class ConversationView : UserControl
{
    private const double Near = 48;

    private bool following = true;

    public ConversationView()
    {
        InitializeComponent();
        PromptBox.AddHandler(KeyDownEvent, Submit, RoutingStrategies.Tunnel);
    }

    private void Submit(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;

        if (DataContext is ConversationViewModel viewModel && viewModel.CraftCommand.CanExecute(null))
        {
            following = true;
            viewModel.CraftCommand.Execute(null);
        }
    }

    private void Follow(object? sender, ScrollChangedEventArgs e)
    {
        var bottom = Scroller.Extent.Height - Scroller.Viewport.Height;

        if (e.ExtentDelta.Y > 0 && following)
        {
            Scroller.ScrollToEnd();
        }
        else if (e.OffsetDelta.Y != 0)
        {
            following = Scroller.Offset.Y >= bottom - Near;
        }

        Jump.IsVisible = !following && bottom > Near;
    }

    private void ToLatest(object? sender, RoutedEventArgs e)
    {
        following = true;
        Scroller.ScrollToEnd();
    }
}
