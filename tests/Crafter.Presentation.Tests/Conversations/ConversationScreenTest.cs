using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Preferences;
using Crafter.Presentation.Conversations;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Conversations;

public sealed class ConversationScreenTest
{
    private readonly FakeTimeProvider time = new();

    private readonly ClipboardInMemory clipboard = new();

    private readonly PreferenceBook preferences = new(new PreferencesInMemory(UserPreferences.Default("/home/hero")));

    [Fact]
    public async Task CountsTheSecondsWhileCrafting()
    {
        var screen = Screen(new ScriptedSession([new Wrote("Thinking")], hangs: true));

        var crafting = Ask(screen, "oi");
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("crafting · 5s", screen.Elapsed);

        screen.Leave();
        await crafting;
    }

    [Fact]
    public async Task CountsInMinutesAfterTheFirstOne()
    {
        var screen = Screen(new ScriptedSession([], hangs: true));

        var crafting = Ask(screen, "oi");
        time.Advance(TimeSpan.FromSeconds(72));

        Assert.Equal("crafting · 1m 12s", screen.Elapsed);

        screen.Leave();
        await crafting;
    }

    [Fact]
    public async Task StopsTheClockWhenTheAnswerEnds()
    {
        var screen = Screen(new ScriptedSession([new Wrote("Done"), new Finished()]));

        await Ask(screen, "oi");
        time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal("crafting · 0s", screen.Elapsed);
        Assert.Equal(ChatState.Unread, screen.State);
    }

    [Fact]
    public async Task CopiesAnAnswerToTheClipboard()
    {
        var screen = Screen(new ScriptedSession([new Wrote("Olá, "), new Wrote("mundo"), new Finished()]));

        await Ask(screen, "oi");
        await screen.Messages.OfType<CrafterMessage>().Single().CopyCommand.ExecuteAsync(null);

        Assert.Equal(["Olá, mundo"], clipboard.Copied);
    }

    [Fact]
    public async Task CopiesACodeBlockOnItsOwn()
    {
        var screen = Screen(new ScriptedSession([new Wrote("Run:\n```bash\njust test\n```"), new Finished()]));

        await Ask(screen, "oi");
        var code = screen.Messages.OfType<CrafterMessage>().Single().Items.OfType<TextSegment>().Single().Pieces.OfType<CodeBlock>().Single();
        await code.CopyCommand.ExecuteAsync(null);

        Assert.Equal(["just test"], clipboard.Copied);
    }

    [Fact]
    public async Task AsksWithThePreferencesOfTheMoment()
    {
        var session = new ScriptedSession([new Finished()]);
        var screen = Screen(session);

        preferences.Configure(new AgentId("stand-in"), Configuration.Empty.With("model", "opus"));
        await Ask(screen, "oi");

        Assert.Equal([Configuration.Empty.With("model", "opus")], session.Configurations);
    }

    private static Task Ask(ConversationScreen screen, string prompt)
    {
        screen.Draft = prompt;

        return screen.CraftCommand.ExecuteAsync(null);
    }

    private ConversationScreen Screen(IAgentSession session)
    {
        var agent = new StandInAgent(() => session);
        var chat = new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session));

        return new ConversationScreen(chat, agent, preferences, "crafter", new KeeperInMemory(), clipboard, time);
    }
}
