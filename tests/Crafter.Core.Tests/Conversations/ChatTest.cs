using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Tests.Conversations;

public sealed class ChatTest
{
    private static readonly AgentId Claude = new("claude-code");

    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IsTitledByItsFirstPrompt()
    {
        var chat = new Chat(ChatId.New(), Claude, ChatTitle.Untitled, new Conversation(new ScriptedSession(new Finished())));

        await chat.Ask(new Prompt("Cover Paces with tests"), Configuration.Empty, Stop).ToListAsync(Stop);
        await chat.Ask(new Prompt("And the bot too"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal("Cover Paces with tests", chat.Title.Text);
    }

    [Fact]
    public async Task KeepsTheTitleOfARestoredChat()
    {
        var history = new Turn[] { new Asked(new Prompt("oi")), new Answered("Olá", new Finished()) };
        var chat = new Chat(ChatId.New(), Claude, ChatTitle.Of("Greetings"), new Conversation(new ScriptedSession(new Finished()), history));

        await chat.Ask(new Prompt("again"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal("Greetings", chat.Title.Text);
    }

    [Fact]
    public async Task RecordsWhatItNeedsToComeBack()
    {
        var id = ChatId.New();
        var chat = new Chat(id, Claude, ChatTitle.Untitled, new Conversation(new ScriptedSession(new Wrote("Olá"), new Finished()) { SessionId = "s-1" }));

        await chat.Ask(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);
        var record = chat.Record();

        Assert.Equal(id, record.Id);
        Assert.Equal(Claude, record.Agent);
        Assert.Equal(ChatTitle.Of("oi"), record.Title);
        Assert.Equal("s-1", record.SessionId);
        Assert.Equal("s-1", chat.SessionId);
        Assert.Same(Configuration.Empty, record.Adjustments);
        Assert.Equal<Turn>([new Asked(new Prompt("oi")), new Answered("Olá", new Finished())], record.Turns);
        Assert.Equal(record.Turns, chat.Turns);
    }

    [Fact]
    public void KeepsItsAdjustmentsInTheRecord()
    {
        var chat = new Chat(ChatId.New(), Claude, ChatTitle.Untitled, new Conversation(new ScriptedSession()));

        chat.Adjust(Configuration.Empty.With("tools", "plan"));

        Assert.Equal("plan", chat.Adjustments["tools"]);
        Assert.Equal("plan", chat.Record().Adjustments["tools"]);
    }

    [Fact]
    public void PassesDecisionsToItsSession()
    {
        var session = new ScriptedSession();
        var chat = new Chat(ChatId.New(), Claude, ChatTitle.Untitled, new Conversation(session));

        chat.Decide("r-1", new AlwaysAllow());

        Assert.Equal([("r-1", (Decision)new AlwaysAllow())], session.Decisions);
    }
}
