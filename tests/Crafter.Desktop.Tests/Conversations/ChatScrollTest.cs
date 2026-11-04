using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Preferences;
using Crafter.Desktop.Conversations;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Composing;
using Crafter.Presentation.Platform;
using Crafter.Testing;

namespace Crafter.Desktop.Tests.Conversations;

// These tests await the agent while pumping the UI thread, which would run other tests' work in
// the middle of theirs, so they run on their own.
[CollectionDefinition(nameof(ChatScrollTest), DisableParallelization = true)]
public sealed class ChatScrollCollection;

[Collection(nameof(ChatScrollTest))]
public sealed class ChatScrollTest : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("crafter-scroll").FullName;

    [Fact]
    public Task LetsGoOfTheEndOnTheSmallestScrollUp()
    {
        return TestApp.RunAsync(async () =>
        {
            var streaming = new StreamingSession();
            var (window, view, chat) = await Chatted(streaming);
            var scroller = Scroller(view);
            var nudged = scroller.Extent.Height - scroller.Viewport.Height - 10;
            scroller.Offset = new Vector(0, nudged);
            Settle();

            chat.Draft = "one more";
            var last = chat.CraftCommand.ExecuteAsync(null);
            Write(streaming, Paragraphs(4));
            streaming.Finish();
            await last;
            Settle();

            Assert.Equal(nudged, scroller.Offset.Y, 1);
            window.Close();
        });
    }

    private async Task<(Window Window, ConversationView View, ConversationViewModel Chat)> Chatted(StreamingSession streaming)
    {
        var shown = Show(streaming);
        for (var turn = 0; turn < 6; turn++)
        {
            shown.Chat.Draft = $"question {turn}";
            var asked = shown.Chat.CraftCommand.ExecuteAsync(null);
            Write(streaming, Paragraphs(turn + 3));
            streaming.Finish();
            await asked;
            Settle();
        }

        return shown;
    }

    private static ScrollViewer Scroller(ConversationView view) => view.GetVisualDescendants().OfType<ScrollViewer>().Single(each => each.Name == "Scroller");

    private static string Paragraphs(int count) =>
        string.Join("\n\n", Enumerable.Range(1, count).Select(at => $"Paragraph {at} " + string.Concat(Enumerable.Repeat("words that wrap around ", 4 + at % 5 * 6))));

    private static void Write(StreamingSession streaming, string text)
    {
        foreach (var piece in text.Chunk(60))
        {
            streaming.Say(new string(piece));
            Settle();
        }
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private (Window Window, ConversationView View, ConversationViewModel Chat) Show(IAgentSession session)
    {
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

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    // Answers with what the test says, piece by piece, so layout runs between the pieces.
    private sealed class StreamingSession : IAgentSession
    {
        private Channel<Reply> replies = Channel.CreateUnbounded<Reply>();

        public string? SessionId => null;

        public void Decide(string request, Decision decision)
        {
        }

        public void Say(string text) => replies.Writer.TryWrite(new Wrote(text));

        public void Finish()
        {
            replies.Writer.TryWrite(new Finished());
            replies.Writer.TryComplete();
        }

        public async IAsyncEnumerable<Reply> Answer(Prompt prompt, Configuration configuration, [EnumeratorCancellation] CancellationToken stop = default)
        {
            var reading = replies;
            await foreach (var reply in reading.Reader.ReadAllAsync(stop))
            {
                yield return reply;
            }

            replies = Channel.CreateUnbounded<Reply>();
        }
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
