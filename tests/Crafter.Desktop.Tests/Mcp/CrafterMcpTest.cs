using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Crafter.Core.Agents;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Desktop.Mcp;
using Crafter.Presentation;
using Crafter.Presentation.Abilities;
using Crafter.Presentation.Agents;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Messages;
using Crafter.Presentation.Desks;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;
using Crafter.Presentation.Platform;
using Crafter.Presentation.Preferences;
using Crafter.Presentation.Projects.Namer;
using Crafter.Presentation.Status;
using Crafter.Presentation.Themes;
using Crafter.Testing;

namespace Crafter.Desktop.Tests.Mcp;

public sealed class CrafterMcpTest : IDisposable
{
    private readonly string home = Directory.CreateTempSubdirectory("crafter-mcp-").FullName;

    // Every session the stand-in agent began, in order; each works until stopped.
    private readonly List<ScriptedSession> begun = [];

    // A session with no chat behind it, for calls that ask nobody.
    private readonly ScriptedSession elsewhere = new();

    [Fact]
    public async Task ServesHomeAndNoProject()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, crafter, lorekeeper) = Workbench();
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();

            var server = Assert.Single(mcp.ServersFor(workbench.Home, elsewhere));
            Assert.Equal(["list_projects", "open_project", "library_status", "sync_library", "list_skills", "read_skill", "propose_axiom", "propose_summary"], server.FreeTools);

            var tools = (await Ask(http, server, "tools/list"))["result"]!["tools"]!.AsArray();
            Assert.Equal(
                ["list_projects", "add_project", "create_project", "open_project", "remove_project", "rename_project", "set_project_icon", "propose_axiom", "propose_summary", "remove_axiom", "library_status", "sync_library", "list_skills", "read_skill", "save_skill"],
                tools.Select(tool => tool!["name"]!.GetValue<string>()));

            var listed = await Ask(http, server, "tools/call", new JsonObject { ["name"] = "list_projects", ["arguments"] = new JsonObject() });
            var projects = JsonNode.Parse(listed["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsArray();
            Assert.Equal(["Crafter", "Lorekeeper"], projects.Select(project => project!["name"]!.GetValue<string>()));

            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(http, server with { Token = "forged" }, "tools/list")).StatusCode);
        });
    }

    [Fact]
    public async Task AProjectsAgentProposesAxiomsTheUserApprovesInItsChat()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, crafter, _) = Workbench();
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();
            workbench.NewChatCommand.Execute(null);
            var chat = workbench.Desk!.Selected!;
            chat.Draft = "Tidy the code";
            _ = chat.CraftCommand.ExecuteAsync(null);

            var server = Assert.Single(mcp.ServersFor(crafter, begun[^1]));
            Assert.Equal(["propose_axiom", "propose_summary"], server.FreeTools);
            Assert.Equal(TimeSpan.FromMinutes(30), server.Patience);
            Assert.NotEqual(mcp.ServersFor(crafter, elsewhere)[0].Token, server.Token);
            var tools = (await Ask(http, server, "tools/list"))["result"]!["tools"]!.AsArray();
            Assert.Equal(["propose_axiom", "propose_summary"], tools.Select(tool => tool!["name"]!.GetValue<string>()));
            var initialized = await Ask(http, server, "initialize", new JsonObject { ["protocolVersion"] = "2025-06-18" });
            Assert.Equal(CrafterMcp.ProjectInstructions, initialized["result"]!["instructions"]!.GetValue<string>());

            var approving = Ask(http, server, "tools/call", Proposal("Never add comments"));
            var first = await Card(chat, 1);
            Assert.Empty(workbench.AxiomsOf(crafter));
            Assert.True(chat.IsWaiting);
            first.ApproveCommand.Execute(null);
            Assert.False((await approving)["result"]!["isError"]!.GetValue<bool>());
            Assert.Equal(["Never add comments"], workbench.AxiomsOf(crafter));

            var declining = Ask(http, server, "tools/call", Proposal("Always write tests"));
            (await Card(chat, 2)).DeclineCommand.Execute(null);
            Assert.Contains("declined", (await declining)["result"]!["content"]![0]!["text"]!.GetValue<string>());
            Assert.Equal(["Never add comments"], workbench.AxiomsOf(crafter));

            var renamed = await Ask(http, server, "tools/call", new JsonObject { ["name"] = "rename_project", ["arguments"] = new JsonObject { ["folder"] = crafter.Folder, ["name"] = "Mine" } });
            Assert.True(renamed["result"]!["isError"]!.GetValue<bool>());

            chat.CraftCancelCommand.Execute(null);
            workbench.Remove(crafter);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(http, server, "tools/list")).StatusCode);
            Assert.Empty(mcp.ServersFor(crafter, begun[^1]));
        });
    }

    [Fact]
    public async Task AProjectsAgentProposesASummaryTheUserApprovesInItsChat()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, crafter, _) = Workbench();
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();
            workbench.Summarize(crafter, "Crafter is my IDE.");
            workbench.NewChatCommand.Execute(null);
            var chat = workbench.Desk!.Selected!;
            chat.Draft = "Learn the project";
            _ = chat.CraftCommand.ExecuteAsync(null);
            var server = Assert.Single(mcp.ServersFor(crafter, begun[^1]));

            var approving = Ask(http, server, "tools/call", new JsonObject
            {
                ["name"] = "propose_summary",
                ["arguments"] = new JsonObject { ["summary"] = "Crafter is my IDE.\nIts tests run with just test." },
            });
            var card = await SummaryCard(chat);
            Assert.Equal("Crafter is my IDE.", workbench.SummaryOf(crafter));
            Assert.Equal(["  Crafter is my IDE.", "+ Its tests run with just test."], card.Lines.Select(line => line.Marked));
            card.ApproveCommand.Execute(null);

            Assert.False((await approving)["result"]!["isError"]!.GetValue<bool>());
            Assert.Equal("Crafter is my IDE.\nIts tests run with just test.", workbench.SummaryOf(crafter));
            chat.CraftCancelCommand.Execute(null);
        });
    }

    [Fact]
    public async Task DeclinesAProposalNoChatCanAnswer()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, crafter, _) = Workbench();
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();

            var proposed = await Ask(http, mcp.ServersFor(crafter, elsewhere)[0], "tools/call", Proposal("Never add comments"));

            Assert.Contains("declined", proposed["result"]!["content"]![0]!["text"]!.GetValue<string>());
            Assert.Empty(workbench.AxiomsOf(crafter));
        });
    }

    [Fact]
    public async Task TellsHomeWhatItIsFor()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, _, _) = Workbench();
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();

            var initialized = await Ask(http, mcp.ServersFor(workbench.Home, elsewhere)[0], "initialize", new JsonObject { ["protocolVersion"] = "2025-06-18" });

            Assert.Equal(CrafterMcp.Instructions, initialized["result"]!["instructions"]!.GetValue<string>());
        });
    }

    [Fact]
    public async Task RenamesAProjectFromAToolCall()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, _, lorekeeper) = Workbench();
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();

            var renamed = await Ask(http, mcp.ServersFor(workbench.Home, elsewhere)[0], "tools/call", new JsonObject
            {
                ["name"] = "rename_project",
                ["arguments"] = new JsonObject { ["folder"] = lorekeeper.Folder, ["name"] = "Lorekeeper · desktop" },
            });

            Assert.False(renamed["result"]!["isError"]!.GetValue<bool>());
            Assert.Contains(workbench.Shelf, item => item.Project.Name.Text == "Lorekeeper · desktop");
        });
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private static JsonObject Proposal(string axiom) =>
        new() { ["name"] = "propose_axiom", ["arguments"] = new JsonObject { ["axiom"] = axiom } };

    private static async Task<SummaryProposalItemViewModel> SummaryCard(ConversationViewModel chat)
    {
        for (var look = 0; look < 500; look++)
        {
            if (chat.Messages.OfType<CrafterMessageViewModel>().SelectMany(answer => answer.Items.OfType<SummaryProposalItemViewModel>()).FirstOrDefault() is { } card)
            {
                return card;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("No summary card showed.");
    }

    // The card shows on the UI thread while the call waits, so this looks again between turns of it.
    private static async Task<AxiomProposalItemViewModel> Card(ConversationViewModel chat, int count)
    {
        for (var look = 0; look < 500; look++)
        {
            var cards = chat.Messages.OfType<CrafterMessageViewModel>().SelectMany(answer => answer.Items.OfType<AxiomProposalItemViewModel>()).ToList();
            if (cards.Count >= count)
            {
                return cards[count - 1];
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("No axiom card showed in the chat.");
    }

    private static async Task<JsonNode> Ask(HttpClient http, ToolServer server, string method, JsonObject? parameters = null)
    {
        var response = await Post(http, server, method, parameters);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient http, ToolServer server, string method, JsonObject? parameters = null)
    {
        var request = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = parameters ?? new JsonObject() };
        using var message = new HttpRequestMessage(HttpMethod.Post, server.Endpoint)
        {
            Content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Token);

        return await http.SendAsync(message);
    }

    private LibraryViewModel Library() =>
        new(new PreferenceBook(new PreferencesInMemory(UserPreferences.Default(home))), new NoLibrary(), TimeProvider.System);

    private (WorkbenchViewModel Workbench, Project Crafter, Project Lorekeeper) Workbench()
    {
        var agent = new ScriptedAgent
        {
            Sessions = () =>
            {
                var session = new ScriptedSession { Hangs = true };
                begun.Add(session);

                return session;
            },
        };
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);
        var clipboard = new Clipboard();
        var preferences = new PreferenceBook(new PreferencesInMemory(new UserPreferences(home) { Agents = [agent.Id] }));
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => DateTimeOffset.Now);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);
        var workbench = new WorkbenchViewModel(
            new Project(Directory.CreateDirectory(Path.Combine(home, "home")).FullName, new ProjectName("Home")),
            bookshelf, preferences, picker, new ProjectNamerViewModel(picker), setup,
            new PreferencesViewModel(preferences, picker, setup, new Themes(), Library()),
            new DeskFactory(new ChatsInMemory(), crew, preferences, clipboard, TimeProvider.System), clipboard,
            new StatusBarViewModel(new NoBranches(), TimeProvider.System));
        var crafter = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "crafter")).FullName, new ProjectName("Crafter"));
        var lorekeeper = bookshelf.Add(Directory.CreateDirectory(Path.Combine(home, "lorekeeper")).FullName, new ProjectName("Lorekeeper"));
        workbench.Open(crafter);

        return (workbench, crafter, lorekeeper);
    }

    private sealed class NoBranches : IBranches
    {
        public string? Of(Project project) => null;
    }

    private sealed class NoLibrary : ILibrary
    {
        public Task<SyncOutcome> Sync(string repository, CancellationToken stop = default) => Task.FromResult<SyncOutcome>(new Synced("0000000"));

        public LibraryContents Read() => LibraryContents.Empty;

        public Task<SyncOutcome> Save(SkillDraft skill, string repository, CancellationToken stop = default) => Task.FromResult<SyncOutcome>(new Synced("0000000"));
    }

    private sealed class Clipboard : IClipboard
    {
        public Task Copy(string text) => Task.CompletedTask;
    }

    private sealed class Themes : IThemeSwitcher
    {
        public Appearance Appearance => Appearance.Dark;

        public event EventHandler? AppearanceChanged
        {
            add { }
            remove { }
        }

        public void Apply(Theme theme)
        {
        }

        public void Assume(Appearance appearance)
        {
        }
    }
}
