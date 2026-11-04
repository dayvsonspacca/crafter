using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Preferences;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Messages;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Conversations;

public sealed class ConversationViewModelTest
{
    private readonly FakeTimeProvider time = new();

    private readonly ClipboardInMemory clipboard = new();

    private readonly PreferenceBook preferences = new(new PreferencesInMemory(UserPreferences.Default("/home/hero")));

    [Fact]
    public async Task CountsTheSecondsWhileCrafting()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Thinking")) { Hangs = true });

        var crafting = Ask(viewModel, "oi");
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("crafting · 5s", viewModel.Elapsed);

        viewModel.Leave();
        await crafting;
    }

    [Fact]
    public async Task CountsInMinutesAfterTheFirstOne()
    {
        var viewModel = ViewModel(new ScriptedSession { Hangs = true });

        var crafting = Ask(viewModel, "oi");
        time.Advance(TimeSpan.FromSeconds(72));

        Assert.Equal("crafting · 1m 12s", viewModel.Elapsed);

        viewModel.Leave();
        await crafting;
    }

    [Fact]
    public async Task StopsTheClockWhenTheAnswerEnds()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Done"), new Finished()));

        await Ask(viewModel, "oi");
        time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal("crafting · 0s", viewModel.Elapsed);
        Assert.Equal(ChatState.Unread, viewModel.State);
    }

    [Fact]
    public async Task CopiesAnAnswerToTheClipboard()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Olá, "), new Wrote("mundo"), new Finished()));

        await Ask(viewModel, "oi");
        await viewModel.Messages.OfType<CrafterMessageViewModel>().Single().CopyCommand.ExecuteAsync(null);

        Assert.Equal(["Olá, mundo"], clipboard.Copied);
    }

    [Fact]
    public async Task CopiesACodeBlockOnItsOwn()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Run:\n```bash\njust test\n```"), new Finished()));

        await Ask(viewModel, "oi");
        var code = viewModel.Messages.OfType<CrafterMessageViewModel>().Single().Items.OfType<TextSegmentViewModel>().Single().Pieces.OfType<CodeBlockViewModel>().Single();
        await code.CopyCommand.ExecuteAsync(null);

        Assert.Equal(["just test"], clipboard.Copied);
    }

    [Fact]
    public async Task AsksWithThePreferencesOfTheMoment()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        preferences.Configure(new AgentId("stand-in"), Configuration.Empty.With("model", "opus"));
        await Ask(viewModel, "oi");

        Assert.Equal([Configuration.Empty.With("model", "opus")], session.Configurations);
    }

    [Fact]
    public async Task SendsALongPasteInFullWhileShowingItsLabel()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);
        var pasted = string.Join('\n', Enumerable.Range(1, 40).Select(line => $"line {line}"));

        var label = viewModel.Paste(pasted);
        await Ask(viewModel, $"Explain {label}");

        Assert.Equal("[Pasted text #1 +39 lines]", label);
        Assert.Equal($"Explain {pasted}", session.Heard.Single().Text);
        Assert.Equal("Explain [Pasted text #1 +39 lines]", viewModel.Messages.OfType<YourMessageViewModel>().Single().Text);
    }

    [Fact]
    public void KeepsAShortPasteAsItIs()
    {
        Assert.Equal("just test", ViewModel(new ScriptedSession()).Paste("just test"));
    }

    [Fact]
    public async Task SendsTheAttachedPictures()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);
        var picture = new Picture("image/png", [1, 2, 3]);

        viewModel.Attach(picture);
        await Ask(viewModel, "What is in it?");

        Assert.Equal(new Prompt("What is in it?", [picture]), session.Heard.Single());
        Assert.Empty(viewModel.Attached);
        Assert.False(viewModel.HasAttached);
    }

    [Fact]
    public async Task SendsAPictureWithoutWords()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.Attach(new Picture("image/png", [1]));
        await viewModel.CraftCommand.ExecuteAsync(null);

        Assert.Equal("(1 picture attached)", session.Heard.Single().Text);
        Assert.False(viewModel.Messages.OfType<YourMessageViewModel>().Single().HasText);
    }

    [Fact]
    public async Task ForgetsARemovedPicture()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.Attach(new Picture("image/png", [1]));
        viewModel.Attach(new Picture("image/png", [2]));
        viewModel.Attached[0].RemoveCommand.Execute(null);
        viewModel.DetachLast();
        await Ask(viewModel, "look");

        Assert.Empty(session.Heard.Single().Pictures);
    }

    [Fact]
    public async Task RetriesTheSamePrompt()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.Attach(new Picture("image/png", [1]));
        await Ask(viewModel, "Look at this");
        viewModel.Draft = "half typed";
        viewModel.Messages.OfType<CrafterMessageViewModel>().Single().RetryCommand.Execute(null);
        await viewModel.CraftCommand.ExecutionTask!;

        Assert.Equal(2, session.Heard.Count);
        Assert.Equal(session.Heard[0], session.Heard[1]);
        Assert.Equal("half typed", viewModel.Draft);
    }

    [Fact]
    public void MentionsAFileInsideTheProjectByItsRelativePath()
    {
        var viewModel = ViewModel(new ScriptedSession());

        Assert.Equal("@src/Prompt.cs ", viewModel.Mention("/home/hero/crafter/src/Prompt.cs"));
        Assert.Equal("@/tmp/shot.png ", viewModel.Mention("/tmp/shot.png"));
    }

    private static Task Ask(ConversationViewModel viewModel, string prompt)
    {
        viewModel.Draft = prompt;

        return viewModel.CraftCommand.ExecuteAsync(null);
    }

    private ConversationViewModel ViewModel(IAgentSession session)
    {
        var agent = new ScriptedAgent { Sessions = () => session };
        var chat = new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session));

        return new ConversationViewModel(chat, agent, preferences, "crafter", new KeeperInMemory(), clipboard, new FilesInMemory("README.md", "src/", "src/Prompt.cs"), time);
    }
}
