using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Crafter.ClaudeCode;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;
using Crafter.Desktop.Conversations;
using Crafter.Desktop.Keyboard;
using Crafter.Desktop.Projects;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop;

public sealed partial class Workbench : Window
{
    private readonly Shortcuts shortcuts = new();

    public Workbench()
        : this(
            WindowManager.Detect(),
            new Bookshelf(JsonBookshelfStore.InUserConfig(), () => DateTimeOffset.Now),
            project => new ConversationScreen(new Conversation(new ClaudeCodeSession(project.Folder))))
    {
    }

    public Workbench(WindowManager windowManager, Bookshelf bookshelf, Func<Project, ConversationScreen> converse)
    {
        InitializeComponent();

        var screen = new WorkbenchScreen(bookshelf, converse, new StorageFolderPicker(this));
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

    private void Listen(WorkbenchScreen screen)
    {
        shortcuts.On(ProjectShortcuts.Open, () => screen.SearchCommand.Execute(null));
        shortcuts.On(ProjectShortcuts.Add, () => screen.AddCommand.Execute(null));
        shortcuts.On(ProjectShortcuts.Close, () => screen.CloseCommand.Execute(null));

        for (var place = 0; place < ProjectShortcuts.Slots; place++)
        {
            var slot = place;
            shortcuts.On(ProjectShortcuts.Slot(slot)!, () => screen.OpenSlot(slot));
        }
    }

    private void Press(object? sender, KeyEventArgs e)
    {
        if (shortcuts.Handle(e))
        {
            e.Handled = true;
        }
    }
}
