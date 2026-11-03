using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Core.Tests.Conversations;

public sealed class JsonChatArchiveTest : IDisposable
{
    private static readonly AgentId Claude = new("claude-code");

    private static readonly AgentId Codex = new("codex");

    private static readonly Project Crafter = new("/home/hero/Projetos/me/desktop/crafter");

    private readonly string folder = Directory.CreateTempSubdirectory("crafter-chats-").FullName;

    [Fact]
    public void StartsEmptyWithoutAFile()
    {
        Assert.Empty(new JsonChatArchive(folder).Load(Crafter));
    }

    [Fact]
    public void LoadsWhatItSaved()
    {
        ChatRecord[] chats =
        [
            new(ChatId.New(), Claude, ChatTitle.Of("Fix reconnect"), "s-1",
            [
                new Asked(new Prompt("Fix it")),
                new Answered("Done", new Finished()),
                new Asked(new Prompt("More")),
                new Answered("Half", new Stopped()),
                new Asked(new Prompt("Long")),
                new Answered("Cut", new CutShort()),
                new Asked(new Prompt("Risky")),
                new Answered(string.Empty, new Refused("cyber")),
                new Asked(new Prompt("Broken")),
                new Answered(string.Empty, new Failed("Not logged in")),
            ]),
            new(ChatId.New(), Codex, ChatTitle.Untitled, null, []) { Adjustments = Configuration.Empty.With("tools", "plan") },
        ];

        new JsonChatArchive(folder).Save(Crafter, chats);
        var loaded = new JsonChatArchive(folder).Load(Crafter);

        Assert.Equal(chats.Length, loaded.Count);
        for (var at = 0; at < chats.Length; at++)
        {
            Assert.Equal(chats[at].Id, loaded[at].Id);
            Assert.Equal(chats[at].Agent, loaded[at].Agent);
            Assert.Equal(chats[at].Title, loaded[at].Title);
            Assert.Equal(chats[at].SessionId, loaded[at].SessionId);
            Assert.Equal(chats[at].Turns, loaded[at].Turns);
            Assert.Equal(chats[at].Adjustments, loaded[at].Adjustments);
        }
    }

    [Fact]
    public void KeepsEachProjectApart()
    {
        var archive = new JsonChatArchive(folder);
        var twin = new Project("/home/hero/elsewhere/crafter");

        archive.Save(Crafter, [new ChatRecord(ChatId.New(), Claude, ChatTitle.Of("Here"), null, [])]);

        Assert.Empty(archive.Load(twin));
    }

    [Fact]
    public void ReadsAnUnknownEndingAsAFailure()
    {
        var archive = new JsonChatArchive(folder);
        archive.Save(Crafter, [new ChatRecord(ChatId.New(), Claude, ChatTitle.Of("Odd"), null, [])]);
        var file = Directory.GetFiles(folder).Single();
        File.WriteAllText(
            file,
            """[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","agent":"claude-code","title":"Odd","sessionId":null,"turns":[{"kind":"answered","text":"?","ending":"vanished","detail":null}]}]""");

        var turn = Assert.Single(Assert.Single(archive.Load(Crafter)).Turns);

        Assert.Equal(new Answered("?", new Failed("The answer could not be read back.")), turn);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","agent":"claude-code","title":" ","sessionId":null,"turns":[]}]""")]
    [InlineData("""[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","title":"No agent","sessionId":null,"turns":[]}]""")]
    public void StartsEmptyWhenTheFileIsUnreadable(string content)
    {
        var archive = new JsonChatArchive(folder);
        archive.Save(Crafter, []);
        File.WriteAllText(Directory.GetFiles(folder).Single(), content);

        Assert.Empty(archive.Load(Crafter));
    }

    [Fact]
    public void LivesInTheUserData()
    {
        Assert.NotNull(JsonChatArchive.InUserData());
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
