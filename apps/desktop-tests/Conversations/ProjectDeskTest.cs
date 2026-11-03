using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;
using Crafter.Core.Settings;
using Crafter.Desktop.Conversations;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Desktop.Tests.Conversations;

public sealed class ProjectDeskTest
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Project Crafter = new("/home/hero/Projetos/crafter");

    [Fact]
    public async Task SaysHowLongAgoAResumedChatWasActive()
    {
        var agent = new StandInAgent { Replays = [new Asked(new Prompt("oi")), new Answered("Olá", new Finished())] };
        var desk = Desk(agent);

        await desk.Resume(agent, new PastChat("s-1", ChatTitle.Of("oi"), Noon.AddHours(-2)));

        Assert.Equal("resumed from Stand-in · 2h ago", desk.Selected!.Messages.OfType<ResumedMark>().Single().Text);
    }

    [Fact]
    public void KeepsANewChatInTheArchive()
    {
        var archive = new ArchiveInMemory();
        var desk = Desk(new StandInAgent(), archive);

        desk.NewChat();

        Assert.Single(archive.Saved);
    }

    [Fact]
    public void RestoresTheAdjustmentsOfASavedChat()
    {
        var archive = new ArchiveInMemory();
        archive.Save(Crafter, [new ChatRecord(ChatId.New(), new AgentId("stand-in"), ChatTitle.Of("oi"), null, []) { Adjustments = Configuration.Empty.With("tools", "plan") }]);

        var desk = Desk(new StandInAgent(), archive);

        Assert.Equal("plan", desk.Chats.Single().Record().Adjustments["tools"]);
    }

    private static ProjectDesk Desk(IAgent agent, ArchiveInMemory? archive = null)
    {
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);

        return new DeskFactory(archive ?? new ArchiveInMemory(), crew, new PreferenceBook(new PreferencesInMemory(Preferences.Default("/home/hero"))), new ClipboardInMemory(), new FakeTimeProvider(Noon)).Make(Crafter, _ => Task.CompletedTask);
    }
}
