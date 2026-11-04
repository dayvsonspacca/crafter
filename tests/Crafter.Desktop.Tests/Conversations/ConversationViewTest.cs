using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Preferences;
using Crafter.Desktop.Conversations;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Platform;
using Crafter.Testing;

namespace Crafter.Desktop.Tests.Conversations;

public sealed class ConversationViewTest : IDisposable
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

    [Fact]
    public void IgnoresAPictureThatArrivesAsBytesInText()
    {
        TestApp.Run(() =>
        {
            var (window, _, chat) = Show();

            Drop(window, Text("\uFFFDPNG\r\n\u001A\n\0\0\0\rIHDR\uFFFD\uFFFDIEND\uFFFDB`\uFFFD"));

            Assert.Empty(chat.Attached);
            Assert.Equal(string.Empty, chat.Draft);
        });
    }

    [Fact]
    public void EscapeTwiceInThePromptClearsTheDraft()
    {
        TestApp.Run(() =>
        {
            var (window, view, chat) = Show();
            var prompt = view.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "PromptBox");
            prompt.Focus();
            chat.Draft = "half a thought";
            Settle();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            Assert.True(chat.IsClearArmed);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Press Esc again to clear" && text.IsEffectivelyVisible);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            Assert.Equal(string.Empty, chat.Draft);
            Assert.Equal(string.Empty, prompt.Text);
        });
    }

    [Fact]
    public void ScrollingUpNearTheTopBringsEarlierMessagesInWithoutMovingTheReader()
    {
        TestApp.Run(() =>
        {
            var exchanges = (ConversationViewModel.Window * 2) + 2;
            Turn[] turns =
            [
                .. Enumerable.Range(1, exchanges).SelectMany(at => new Turn[]
                {
                    new Asked(new Prompt($"Ask {at}")),
                    new Answered(string.Join("\n\n", Enumerable.Repeat($"Answer {at}, long enough to take some room on screen.", 6)), new Finished()),
                }),
            ];
            var (_, view, chat) = Show(turns);
            var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().Single(each => each.Name == "Scroller");
            scroller.ScrollToEnd();
            Settle();
            var top = chat.Messages[0];
            var onScreen = Where(view, scroller, top) + scroller.Offset.Y - 100;

            scroller.Offset = new Vector(0, 100);
            Settle();
            Settle();

            Assert.Equal(ConversationViewModel.Window * 4, chat.Messages.Count);
            Assert.True(chat.HasEarlier);
            Assert.InRange(Where(view, scroller, top), onScreen - 2, onScreen + 2);
        });
    }

    // Taking answers down once cost more for each one still on screen: twenty took seconds.
    [Fact]
    public void TakesManyAnswersDownQuickly()
    {
        TestApp.Run(() =>
        {
            Turn[] turns = [.. Enumerable.Range(1, 20).SelectMany(at => new Turn[] { new Asked(new Prompt($"Ask {at}")), new Answered($"Answer {at}.", new Finished()) })];
            var (_, view, chat) = Show(turns);
            while (chat.HasEarlier)
            {
                chat.ShowEarlierCommand.Execute(null);
            }

            Settle();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            view.DataContext = null;
            Settle();

            Assert.InRange(watch.ElapsedMilliseconds, 0, 1000);
        });
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private static double Where(ConversationView view, ScrollViewer scroller, object message) =>
        view.GetVisualDescendants().OfType<Control>().First(each => each.DataContext == message && each.Parent is Panel).TranslatePoint(default, scroller)!.Value.Y;

    private (Window Window, ConversationView View, ConversationViewModel Chat) Show(params Turn[] turns)
    {
        var session = new ScriptedSession();
        var agent = new ScriptedAgent { Sessions = () => session };
        var chat = new ConversationViewModel(
            new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session, turns)),
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
