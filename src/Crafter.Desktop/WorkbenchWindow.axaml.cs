using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Crafter.Desktop.Keyboard;
using Crafter.Desktop.WindowManagers;
using Crafter.Presentation;
using Crafter.Presentation.Keyboard;

namespace Crafter.Desktop;

public sealed partial class WorkbenchWindow : Window
{
    private readonly ShortcutHandler shortcuts = new();

    private bool leaving;

    public WorkbenchWindow(WindowManager windowManager, WorkbenchViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        Bar.ArrangeFor(windowManager);
        Listen(viewModel);
        AddHandler(KeyDownEvent, Press, RoutingStrategies.Tunnel);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            Bar?.Follow(WindowState);
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (leaving || DataContext is not WorkbenchViewModel { IsCrafting: true } viewModel)
        {
            return;
        }

        e.Cancel = true;
        leaving = true;
        await viewModel.LeaveAsync();
        Close();
    }

    private void Listen(WorkbenchViewModel viewModel)
    {
        shortcuts.On(ProjectShortcuts.Add, () => viewModel.AddCommand.Execute(null));
        shortcuts.On(ProjectShortcuts.Close, () => viewModel.CloseCommand.Execute(null));
        shortcuts.On(ProjectShortcuts.Edit, () => viewModel.EditCurrentCommand.Execute(null));
        shortcuts.On(WorkbenchShortcuts.ToggleSidebar, () => viewModel.ToggleSidebarCommand.Execute(null));
        shortcuts.On(WorkbenchShortcuts.TogglePreferences, () => viewModel.TogglePreferencesCommand.Execute(null));
        shortcuts.On(ChatShortcuts.New, () => viewModel.NewChatCommand.Execute(null));
        shortcuts.On(ChatShortcuts.Close, () => viewModel.CloseChatCommand.Execute(null));
        shortcuts.On(ChatShortcuts.Next, () => viewModel.NextChatCommand.Execute(null));
        shortcuts.On(ChatShortcuts.Previous, () => viewModel.PreviousChatCommand.Execute(null));

        for (var place = 0; place < ProjectShortcuts.Slots; place++)
        {
            var slot = place;
            shortcuts.On(ProjectShortcuts.Slot(slot)!, () => viewModel.OpenSlot(slot));
        }
    }

    private void Press(object? sender, KeyEventArgs e)
    {
        if (DataContext is WorkbenchViewModel { ShowsSetup: false } && shortcuts.Handle(e))
        {
            e.Handled = true;
        }
    }
}
