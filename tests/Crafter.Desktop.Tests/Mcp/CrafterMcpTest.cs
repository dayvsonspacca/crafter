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

    [Fact]
    public async Task ServesTheGrantedProjectAndStopsTheMomentTheGrantGoes()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, crafter, lorekeeper) = Workbench();
            workbench.Allow(crafter, Capability.ManageCrafter, allowed: true);
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();

            Assert.Empty(mcp.ServersFor(lorekeeper));
            var server = Assert.Single(mcp.ServersFor(crafter));
            Assert.Equal(["list_projects", "library_status", "sync_library", "list_skills", "read_skill"], server.FreeTools);

            var tools = (await Ask(http, server, "tools/list"))["result"]!["tools"]!.AsArray();
            Assert.Equal(7, tools.Count);

            var listed = await Ask(http, server, "tools/call", new JsonObject { ["name"] = "list_projects", ["arguments"] = new JsonObject() });
            var projects = JsonNode.Parse(listed["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsArray();
            Assert.Equal(["Crafter", "Lorekeeper"], projects.Select(project => project!["name"]!.GetValue<string>()));
            Assert.True(projects[0]!["isYours"]!.GetValue<bool>());

            workbench.Allow(crafter, Capability.ManageCrafter, allowed: false);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(http, server, "tools/list")).StatusCode);
            Assert.Empty(mcp.ServersFor(crafter));
        });
    }

    [Fact]
    public async Task RenamesAProjectFromAToolCall()
    {
        await TestApp.RunAsync(async () =>
        {
            var (workbench, crafter, lorekeeper) = Workbench();
            workbench.Allow(crafter, Capability.ManageCrafter, allowed: true);
            using var mcp = new CrafterMcp(workbench, new CrafterAbilities(workbench, Library(), new NoBranches()));
            mcp.Start();
            using var http = new HttpClient();

            var renamed = await Ask(http, mcp.ServersFor(crafter)[0], "tools/call", new JsonObject
            {
                ["name"] = "rename_project",
                ["arguments"] = new JsonObject { ["folder"] = lorekeeper.Folder, ["name"] = "Lorekeeper · desktop" },
            });

            Assert.False(renamed["result"]!["isError"]!.GetValue<bool>());
            Assert.Contains(workbench.Shelf, item => item.Project.Name.Text == "Lorekeeper · desktop");
        });
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

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
        var agent = new ScriptedAgent();
        var crew = new Crew([agent]);
        crew.Hire([agent.Id]);
        var clipboard = new Clipboard();
        var preferences = new PreferenceBook(new PreferencesInMemory(new UserPreferences(home) { Agents = [agent.Id] }));
        var bookshelf = new Bookshelf(new ShelfInMemory(), new SettingsInMemory(), () => DateTimeOffset.Now);
        var picker = new FolderPickerViewModel(preferences, bookshelf);
        var setup = new SetupViewModel(crew, preferences, clipboard);
        var workbench = new WorkbenchViewModel(
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
