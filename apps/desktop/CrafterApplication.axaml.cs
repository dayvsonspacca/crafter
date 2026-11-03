using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Crafter.ClaudeCode;
using Crafter.Core.Conversations;
using Crafter.Desktop.Conversations;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop;

public sealed partial class CrafterApplication : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            var claudeCode = new ClaudeCodeSession(Environment.CurrentDirectory);
            var conversation = new ConversationScreen(new Conversation(claudeCode));

            lifetime.MainWindow = new Workbench(WindowManager.Detect(), conversation);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
