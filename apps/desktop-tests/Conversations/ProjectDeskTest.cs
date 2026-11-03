using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;
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

    private static ProjectDesk Desk(IAgent agent, ArchiveInMemory? archive = null)
    {
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);

        return new DeskFactory(archive ?? new ArchiveInMemory(), crew, new ClipboardInMemory(), new FakeTimeProvider(Noon)).Make(Crafter, _ => Task.CompletedTask);
    }
}
