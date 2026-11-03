using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Crafter.Desktop.Conversations;
using Crafter.Desktop.Keyboard;
using Crafter.Desktop.Projects;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop;

public sealed partial class Workbench : Window
{
    private readonly Shortcuts shortcuts = new();

    private bool leaving;

    public Workbench(WindowManager windowManager, WorkbenchScreen screen)
    {
        InitializeComponent();

        DataContext = screen;
        Bar.ArrangeFor(windowManager);
        Listen(screen);
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

        if (leaving || DataContext is not WorkbenchScreen { IsCrafting: true } screen)
        {
            return;
        }

        e.Cancel = true;
        leaving = true;
        await screen.LeaveAsync();
        Close();
    }

    private void Listen(WorkbenchScreen screen)
    {
        shortcuts.On(ProjectShortcuts.Add, () => screen.AddCommand.Execute(null));
        shortcuts.On(ProjectShortcuts.Close, () => screen.CloseCommand.Execute(null));
        shortcuts.On(ProjectShortcuts.Edit, () => screen.EditCurrentCommand.Execute(null));
        shortcuts.On(WorkbenchShortcuts.ToggleSidebar, () => screen.ToggleSidebarCommand.Execute(null));
        shortcuts.On(WorkbenchShortcuts.TogglePreferences, () => screen.TogglePreferencesCommand.Execute(null));
        shortcuts.On(ChatShortcuts.New, () => screen.NewChatCommand.Execute(null));
        shortcuts.On(ChatShortcuts.Close, () => screen.CloseChatCommand.Execute(null));
        shortcuts.On(ChatShortcuts.Next, () => screen.NextChatCommand.Execute(null));
        shortcuts.On(ChatShortcuts.Previous, () => screen.PreviousChatCommand.Execute(null));

        for (var place = 0; place < ProjectShortcuts.Slots; place++)
        {
            var slot = place;
            shortcuts.On(ProjectShortcuts.Slot(slot)!, () => screen.OpenSlot(slot));
        }
    }

    private void Press(object? sender, KeyEventArgs e)
    {
        if (DataContext is WorkbenchScreen { ShowsSetup: false } && shortcuts.Handle(e))
        {
            e.Handled = true;
        }
    }
}
