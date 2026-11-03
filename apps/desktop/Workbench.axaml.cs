using Avalonia;
using Avalonia.Controls;
using Crafter.ClaudeCode;
using Crafter.Core.Conversations;
using Crafter.Desktop.Conversations;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop;

public sealed partial class Workbench : Window
{
    public Workbench()
        : this(WindowManager.Detect(), new ConversationScreen(new Conversation(new ClaudeCodeSession(Environment.CurrentDirectory))))
    {
    }

    public Workbench(WindowManager windowManager, ConversationScreen conversation)
    {
        InitializeComponent();
        DataContext = conversation;
        Bar.ArrangeFor(windowManager);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            Bar?.Follow(WindowState);
        }
    }
}
