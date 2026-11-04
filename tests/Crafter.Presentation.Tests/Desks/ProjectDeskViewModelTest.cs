using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Conversations.Messages;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Desks;

public sealed class ProjectDeskViewModelTest
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Project Crafter = new("/home/hero/Projetos/crafter");

    [Fact]
    public async Task SaysHowLongAgoAResumedChatWasActive()
    {
        var agent = new ScriptedAgent { Replays = [new Asked(new Prompt("oi")), new Answered("Olá", new Finished())] };
        var desk = Desk(agent);

        await desk.Resume(agent, new PastChat("s-1", ChatTitle.Of("oi"), Noon.AddHours(-2)));

        Assert.Equal("resumed from stand-in · 2h ago", desk.Selected!.Messages.OfType<ResumedMarkViewModel>().Single().Text);
    }

    [Fact]
    public void KeepsANewChatInTheArchive()
    {
        var store = new ChatsInMemory();
        var desk = Desk(new ScriptedAgent(), store);

        desk.NewChat();

        Assert.Single(store.Saved);
    }

    [Fact]
    public void RestoresTheAdjustmentsOfASavedChat()
    {
        var store = new ChatsInMemory();
        store.Save(Crafter, [new ChatRecord(ChatId.New(), new AgentId("stand-in"), ChatTitle.Of("oi"), null, []) { Adjustments = Configuration.Empty.With("tools", "plan") }]);

        var desk = Desk(new ScriptedAgent(), store);

        Assert.Equal("plan", desk.Chats.Single().Record().Adjustments["tools"]);
    }

    private static ProjectDeskViewModel Desk(IAgent agent, ChatsInMemory? store = null)
    {
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);

        return new DeskFactory(store ?? new ChatsInMemory(), crew, new PreferenceBook(new PreferencesInMemory(UserPreferences.Default("/home/hero"))), new ClipboardInMemory(), new FakeTimeProvider(Noon)).Make(Crafter, _ => Task.CompletedTask);
    }
}
