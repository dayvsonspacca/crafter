using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crafter.Core.Conversations;
using Crafter.Core.Preferences;
using Crafter.Desktop.Conversations;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Platform;
using Crafter.Testing;

namespace Crafter.Desktop.Tests.Conversations;

public sealed class ConversationDropTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-drop").FullName;

    // Over the messages, far from the composer: the whole chat takes the drop.
    private static readonly Point OverTheChat = new(300, 120);

    [Fact]
    public void ShowsTheDropZoneWhileFilesHoverAndHidesItWhenTheyLeave()
    {
        TestApp.Run(() =>
        {
            var (window, view, _) = Show();
            var data = Text("file:///tmp/whatever.log");

            window.DragDrop(OverTheChat, RawDragEventType.DragEnter, data, DragDropEffects.Copy);
            Settle();
            Assert.True(Zone(view).IsVisible);

            window.DragDrop(OverTheChat, RawDragEventType.DragLeave, data, DragDropEffects.Copy);
            Settle();
            Assert.False(Zone(view).IsVisible);
        });
    }

    [Fact]
    public void AttachesDroppedFiles()
    {
        TestApp.Run(() =>
        {
            var (window, view, chat) = Show();
            var log = Touch("install.log");
            var file = window.StorageProvider.TryGetFileFromPathAsync(new Uri(log)).GetAwaiter().GetResult();
            Assert.NotNull(file);
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(file));

            Drop(window, data);

            Assert.Equal(["install.log"], chat.Attached.Select(each => each.Name));
            Assert.False(Zone(view).IsVisible);
        });
    }

    [Fact]
    public void AttachesFilesThatArriveAsUrisInText()
    {
        TestApp.Run(() =>
        {
            var (window, _, chat) = Show();
            var log = Touch("install.log");
            var notes = Touch("my notes.txt");

            Drop(window, Text($"{new Uri(log).AbsoluteUri}\r\n{new Uri(notes).AbsoluteUri}\r\n"));

            Assert.Equal(["install.log", "my notes.txt"], chat.Attached.Select(each => each.Name));
            Assert.Equal(string.Empty, chat.Draft);
        });
    }

    [Fact]
    public void TypesDroppedTextIntoTheComposer()
    {
        TestApp.Run(() =>
        {
            var (window, _, chat) = Show();

            Drop(window, Text("fix the table"));

            Assert.Empty(chat.Attached);
            Assert.Equal("fix the table", chat.Draft);
        });
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private (Window Window, ConversationView View, ConversationViewModel Chat) Show()
    {
        var session = new ScriptedSession();
        var agent = new ScriptedAgent { Sessions = () => session };
        var chat = new ConversationViewModel(
            new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session)),
            agent,
            new PreferenceBook(new PreferencesInMemory(UserPreferences.Default(folder))),
            "crafter",
            new Keeper(),
            new Clipboard(),
            new Files(folder),
            TimeProvider.System);
        var view = new ConversationView { DataContext = chat };
        var window = new Window { Width = 820, Height = 600, Content = view };
        window.Show();
        Settle();

        return (window, view, chat);
    }

    private static void Drop(Window window, IDataTransfer data)
    {
        window.DragDrop(OverTheChat, RawDragEventType.DragEnter, data, DragDropEffects.Copy);
        window.DragDrop(OverTheChat, RawDragEventType.DragOver, data, DragDropEffects.Copy);
        window.DragDrop(OverTheChat, RawDragEventType.Drop, data, DragDropEffects.Copy);
        Settle();
    }

    private static DataTransfer Text(string text)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(text));

        return data;
    }

    private static Panel Zone(ConversationView view) => view.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "DropZone");

    private string Touch(string name)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, "x");

        return path;
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Keeper : IChatKeeper
    {
        public void Select(ConversationViewModel chat)
        {
        }

        public void Close(ConversationViewModel chat)
        {
        }

        public void Keep(ConversationViewModel chat)
        {
        }

        public void TogglePanel()
        {
        }
    }

    private sealed class Clipboard : IClipboard
    {
        public Task Copy(string text) => Task.CompletedTask;
    }

    private sealed class Files(string folder) : IProjectFiles
    {
        public string Folder => folder;

        public Task<IReadOnlyList<string>> List(CancellationToken stop = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
