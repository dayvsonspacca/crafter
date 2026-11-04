using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Crafter.Presentation.Conversations;

namespace Crafter.Desktop.Conversations;

// The chat's background jobs in a window over everything, opened from the status bar or Ctrl+J.
public sealed partial class JobsView : UserControl
{
    public JobsView()
    {
        InitializeComponent();
    }

    private ConversationViewModel? Chat => DataContext as ConversationViewModel;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Taking the focus lets Escape close the window.
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
            e.Handled = true;
            Chat?.CloseJobsCommand.Execute(null);
        }
    }

    private void Dismiss(object? sender, RoutedEventArgs e) => Chat?.CloseJobsCommand.Execute(null);
}
