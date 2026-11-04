using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Projects;
using Crafter.Storage;

namespace Crafter.Storage.Tests;

public sealed class JsonChatStoreTest : IDisposable
{
    private static readonly AgentId Claude = new("claude-code");

    private static readonly AgentId Codex = new("codex");

    private static readonly Project Crafter = new("/home/hero/Projetos/me/desktop/crafter");

    private readonly string folder = Directory.CreateTempSubdirectory("crafter-chats-").FullName;

    [Fact]
    public void StartsEmptyWithoutAFile()
    {
        Assert.Empty(new JsonChatStore(folder).Load(Crafter));
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
                new Asked(new Prompt("Run the tests")),
                new Answered("Running them.All green.", new Finished())
                {
                    Steps =
                    [
                        new Wrote("Running them."),
                        new Acting("t-1", new Tool(ToolKind.Shell, "Bash"), "dotnet test"),
                        new Acted("t-1", Failed: false, Output: "Passed: 12"),
                        new Acting("t-2", new Tool(ToolKind.Edit, "Edit"), "src/App.cs", new Changes(2, 1)),
                        new Acted("t-2", Failed: true, Output: "no match"),
                        new Wrote("All green."),
                    ],
                },
            ]),
            new(ChatId.New(), Codex, ChatTitle.Untitled, null, []),
        ];

        new JsonChatStore(folder).Save(Crafter, chats);
        var loaded = new JsonChatStore(folder).Load(Crafter);

        Assert.Equal(chats.Length, loaded.Count);
        for (var at = 0; at < chats.Length; at++)
        {
            Assert.Equal(chats[at].Id, loaded[at].Id);
            Assert.Equal(chats[at].Agent, loaded[at].Agent);
            Assert.Equal(chats[at].Title, loaded[at].Title);
            Assert.Equal(chats[at].SessionId, loaded[at].SessionId);
            Assert.Equal(chats[at].Turns, loaded[at].Turns);
        }
    }

    [Fact]
    public void KeepsEachProjectApart()
    {
        var store = new JsonChatStore(folder);
        var twin = new Project("/home/hero/elsewhere/crafter");

        store.Save(Crafter, [new ChatRecord(ChatId.New(), Claude, ChatTitle.Of("Here"), null, [])]);

        Assert.Empty(store.Load(twin));
    }

    [Fact]
    public void ReadsAnUnknownEndingAsAFailure()
    {
        var store = new JsonChatStore(folder);
        store.Save(Crafter, [new ChatRecord(ChatId.New(), Claude, ChatTitle.Of("Odd"), null, [])]);
        var file = Directory.GetFiles(folder).Single();
        File.WriteAllText(
            file,
            """[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","agent":"claude-code","title":"Odd","sessionId":null,"turns":[{"kind":"answered","text":"?","ending":"vanished","detail":null}]}]""");

        var turn = Assert.Single(Assert.Single(store.Load(Crafter)).Turns);

        Assert.Equal(new Answered("?", new Failed("The answer could not be read back.")), turn);
    }

    [Fact]
    public void ReadsTheLineCountsOfChatsSavedAsOneLine()
    {
        var store = new JsonChatStore(folder);
        store.Save(Crafter, [new ChatRecord(ChatId.New(), Claude, ChatTitle.Of("Old"), null, [])]);
        File.WriteAllText(
            Directory.GetFiles(folder).Single(),
            """[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","agent":"claude-code","title":"Old","sessionId":null,"turns":[{"kind":"answered","text":"","ending":"finished","detail":null,"steps":[{"kind":"acting","id":"t-1","tool":"Edit","summary":"a.cs","stat":"+2 −1"},{"kind":"acting","id":"t-2","tool":"Write","summary":"b.md","stat":"+3"}]}]}]""");

        var answered = Assert.IsType<Answered>(Assert.Single(Assert.Single(store.Load(Crafter)).Turns));

        Assert.Equal([new Acting("t-1", new Tool(ToolKind.Other, "Edit"), "a.cs", new Changes(2, 1)), new Acting("t-2", new Tool(ToolKind.Other, "Write"), "b.md", new Changes(3))], answered.Steps);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","agent":"claude-code","title":" ","sessionId":null,"turns":[]}]""")]
    [InlineData("""[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","title":"No agent","sessionId":null,"turns":[]}]""")]
    public void StartsEmptyWhenTheFileIsUnreadable(string content)
    {
        var store = new JsonChatStore(folder);
        store.Save(Crafter, []);
        File.WriteAllText(Directory.GetFiles(folder).Single(), content);

        Assert.Empty(store.Load(Crafter));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""[{"id":"6f0c8a4e-3a51-4c1e-9a52-1d2b3c4d5e6f","agent":"claude-code","title":" ","sessionId":null,"turns":[]}]""")]
    public void KeepsAnUnreadableFileAside(string content)
    {
        var store = new JsonChatStore(folder);
        store.Save(Crafter, []);
        var file = Directory.GetFiles(folder).Single();
        File.WriteAllText(file, content);

        store.Load(Crafter);
        store.Save(Crafter, []);

        Assert.Equal(content, File.ReadAllText(Aside.Of(file)));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
