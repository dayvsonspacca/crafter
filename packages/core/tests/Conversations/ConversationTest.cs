using Crafter.Core.Conversations;

namespace Crafter.Core.Tests.Conversations;

public sealed class ConversationTest
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ForwardsEveryReplyOfTheAgent()
    {
        Reply[] replies = [new Began("claude-opus-5-5"), new Wrote("Olá"), new Finished()];
        var conversation = new Conversation(new ScriptedAgent(replies));

        var heard = await conversation.Ask(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal(replies, heard);
    }

    [Fact]
    public async Task HandsThePromptToTheAgent()
    {
        var agent = new ScriptedAgent(new Finished());
        var conversation = new Conversation(agent);

        await conversation.Ask(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal([new Prompt("oi")], agent.Heard);
    }

    [Fact]
    public async Task RemembersTheQuestionAndTheAnswer()
    {
        var conversation = new Conversation(new ScriptedAgent(new Wrote("Olá, "), new Wrote("mundo"), new Finished()));

        await conversation.Ask(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal<Turn>([new Asked(new Prompt("oi")), new Answered("Olá, mundo", new Finished())], conversation.Turns);
    }

    [Fact]
    public async Task KeepsWhatWasWrittenWhenStopped()
    {
        var conversation = new Conversation(new ScriptedAgent(new Wrote("pela metade"), new Stopped()));

        await conversation.Ask(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal(new Answered("pela metade", new Stopped()), conversation.Turns[^1]);
    }

    [Fact]
    public async Task KeepsTheRefusalAsTheEnding()
    {
        var conversation = new Conversation(new ScriptedAgent(new Refused("cyber")));

        await conversation.Ask(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal(new Answered(string.Empty, new Refused("cyber")), conversation.Turns[^1]);
    }

    [Fact]
    public async Task EndsWithAFailureWhenTheAgentGoesSilent()
    {
        var conversation = new Conversation(new ScriptedAgent(new Wrote("sem fim")));

        var heard = await conversation.Ask(new Prompt("oi"), Stop).ToListAsync(Stop);

        var failed = Assert.IsType<Failed>(heard[^1]);
        Assert.Equal(new Answered("sem fim", failed), conversation.Turns[^1]);
    }
}
