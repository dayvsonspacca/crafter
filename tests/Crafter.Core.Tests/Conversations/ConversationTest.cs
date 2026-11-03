using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Tests.Conversations;

public sealed class ConversationTest
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ForwardsEveryReplyOfTheAgent()
    {
        Reply[] replies = [new Began("claude-opus-5-5"), new Wrote("Olá"), new Finished()];
        var conversation = new Conversation(new ScriptedSession(replies));

        var heard = await conversation.Ask(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal(replies, heard);
    }

    [Fact]
    public async Task HandsThePromptToTheAgent()
    {
        var agent = new ScriptedSession(new Finished());
        var conversation = new Conversation(agent);

        await conversation.Ask(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal([new Prompt("oi")], agent.Heard);
    }

    [Fact]
    public async Task AsksWithItsAdjustmentsOverThePreferred()
    {
        var agent = new ScriptedSession(new Finished());
        var conversation = new Conversation(agent, [], Configuration.Empty.With("tools", "plan"));

        await conversation.Ask(new Prompt("oi"), Configuration.Empty.With("model", "opus").With("tools", "chat"), Stop).ToListAsync(Stop);

        Assert.Equal([Configuration.Empty.With("model", "opus").With("tools", "plan")], agent.Configurations);
    }

    [Fact]
    public async Task AsksWithThePreferencesOfTheMoment()
    {
        var agent = new ScriptedSession(new Finished());
        var conversation = new Conversation(agent);

        await conversation.Ask(new Prompt("oi"), Configuration.Empty.With("model", "opus"), Stop).ToListAsync(Stop);
        await conversation.Ask(new Prompt("e agora?"), Configuration.Empty.With("model", "sonnet"), Stop).ToListAsync(Stop);

        Assert.Equal([Configuration.Empty.With("model", "opus"), Configuration.Empty.With("model", "sonnet")], agent.Configurations);
    }

    [Fact]
    public void StartsWithTheAdjustmentsItWasGiven()
    {
        var conversation = new Conversation(new ScriptedSession(), [], Configuration.Empty.With("tools", "plan"));

        Assert.Equal("plan", conversation.Adjustments["tools"]);
    }

    [Fact]
    public async Task RemembersTheQuestionAndTheAnswer()
    {
        var conversation = new Conversation(new ScriptedSession(new Wrote("Olá, "), new Wrote("mundo"), new Finished()));

        await conversation.Ask(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal<Turn>([new Asked(new Prompt("oi")), new Answered("Olá, mundo", new Finished())], conversation.Turns);
    }

    [Fact]
    public async Task KeepsWhatWasWrittenWhenStopped()
    {
        var conversation = new Conversation(new ScriptedSession(new Wrote("pela metade"), new Stopped()));

        await conversation.Ask(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal(new Answered("pela metade", new Stopped()), conversation.Turns[^1]);
    }

    [Fact]
    public async Task KeepsTheRefusalAsTheEnding()
    {
        var conversation = new Conversation(new ScriptedSession(new Refused("cyber")));

        await conversation.Ask(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal(new Answered(string.Empty, new Refused("cyber")), conversation.Turns[^1]);
    }

    [Fact]
    public async Task ContinuesFromItsHistory()
    {
        Turn[] history = [new Asked(new Prompt("oi")), new Answered("Olá", new Finished())];
        var conversation = new Conversation(new ScriptedSession(new Wrote("De novo"), new Finished()), history);

        await conversation.Ask(new Prompt("de novo?"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal(
            [.. history, new Asked(new Prompt("de novo?")), new Answered("De novo", new Finished())],
            conversation.Turns);
    }

    [Fact]
    public void KnowsTheSessionOfItsAgent()
    {
        var conversation = new Conversation(new ScriptedSession { SessionId = "s-1" });

        Assert.Equal("s-1", conversation.SessionId);
    }

    [Fact]
    public async Task EndsWithAFailureWhenTheAgentGoesSilent()
    {
        var conversation = new Conversation(new ScriptedSession(new Wrote("sem fim")));

        var heard = await conversation.Ask(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        var failed = Assert.IsType<Failed>(heard[^1]);
        Assert.Equal(new Answered("sem fim", failed), conversation.Turns[^1]);
    }

    [Fact]
    public async Task PassesWhatTheAgentDoesAlongWithoutWritingIt()
    {
        var conversation = new Conversation(new ScriptedSession(
            new Acting("t-1", "Bash", "dotnet test"),
            new NeedsApproval("r-1", "Bash", "dotnet test", "Run the tests"),
            new Acted("t-1", false, "ok"),
            new Wrote("Pronto."),
            new Finished()));

        var replies = await conversation.Ask(new Prompt("testa"), Configuration.Empty, TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, replies.Count);
        Assert.Equal(new Answered("Pronto.", new Finished()), conversation.Turns[^1]);
    }
}
