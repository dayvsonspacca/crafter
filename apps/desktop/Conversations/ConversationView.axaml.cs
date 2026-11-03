using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Crafter.Desktop.Conversations;

public sealed partial class ConversationView : UserControl
{
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

        if (DataContext is ConversationScreen screen && screen.CraftCommand.CanExecute(null))
        {
            screen.CraftCommand.Execute(null);
        }
    }

    private void Follow(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta.Y > 0)
        {
            Scroller.ScrollToEnd();
        }
    }
}
