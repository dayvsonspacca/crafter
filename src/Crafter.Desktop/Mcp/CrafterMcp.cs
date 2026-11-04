using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Crafter.Core.Agents;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Mcp;
using Crafter.Presentation;
using Crafter.Presentation.Abilities;

namespace Crafter.Desktop.Mcp;

// Crafter as a tool server for the agents of projects granted ManageCrafter. Each such project
// gets its own token; the token stops working the moment the grant goes, even mid-chat. Tools run
// on the UI thread, so the sidebar and the library show what they did right away.
public sealed class CrafterMcp : IDisposable
{
    public const string Name = "crafter";

    private static readonly string[] Free = ["list_projects", "library_status", "sync_library", "list_skills", "read_skill"];

    private readonly WorkbenchViewModel workbench;

    private readonly CrafterAbilities abilities;

    private readonly ConcurrentDictionary<string, string> tokens = new();

    private readonly McpServer server;

    public CrafterMcp(WorkbenchViewModel workbench, CrafterAbilities abilities)
    {
        this.workbench = workbench;
        this.abilities = abilities;
        server = new McpServer(Name, "1.0", Tools(), CallerOf);
    }

    public void Start() => server.Start();

    public IReadOnlyList<ToolServer> ServersFor(Project project) =>
        server.Endpoint is { } endpoint && OnUi(() => workbench.Can(project, Capability.ManageCrafter))
            ? [new ToolServer(Name, endpoint, TokenFor(project), Free)]
            : [];

    public void Dispose() => server.Dispose();

    private string TokenFor(Project project)
    {
        foreach (var (token, folder) in tokens)
        {
            if (folder == project.Folder)
            {
                return token;
            }
        }

        var issued = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        tokens[issued] = project.Folder;

        return issued;
    }

    private string? CallerOf(string token) =>
        tokens.TryGetValue(token, out var folder)
        && OnUi(() => workbench.Projects.FirstOrDefault(project => project.Folder == folder) is { } project && workbench.Can(project, Capability.ManageCrafter))
            ? folder
            : null;

    private McpTool[] Tools() =>
    [
        new(
            "list_projects",
            "Lists the projects in Crafter's sidebar: name, folder, git branch, open chats, whether each may manage Crafter, and which one is yours, the project this chat belongs to.",
            Schema(),
            ReadOnly: true,
            call => Run(() => abilities.ListProjects(call.Caller))),
        new(
            "rename_project",
            "Renames a project in Crafter. The name shows in the sidebar and tabs and is kept in the project's .crafter/settings.json.",
            Schema(("folder", "The project's folder, as list_projects gives it.", true), ("name", "The new name.", true)),
            ReadOnly: false,
            call => Run(() => abilities.RenameProject(call.Text("folder"), call.Text("name")))),
        new(
            "library_status",
            "The skills library: its repository, when it last synced and at which commit, how many skills it holds and the folders it could not read.",
            Schema(),
            ReadOnly: true,
            _ => Run(abilities.LibraryStatus)),
        new(
            "sync_library",
            "Brings the skills library up to date with its repository now. Crafter also syncs it on its own every 15 minutes.",
            Schema(),
            ReadOnly: false,
            _ => Run(abilities.SyncLibrary)),
        new(
            "list_skills",
            "Lists the skills in the library: id, name, description and when to use each.",
            Schema(),
            ReadOnly: true,
            _ => Run(abilities.ListSkills)),
        new(
            "read_skill",
            "Reads one skill from the library, with its full instructions.",
            Schema(("id", "The skill's id, as list_skills gives it.", true)),
            ReadOnly: true,
            call => Run(() => abilities.ReadSkill(call.Text("id")))),
        new(
            "save_skill",
            "Creates a skill in the library, or replaces the one with the same id, then commits and pushes it to the main branch of the library repository. "
                + "A skill is know-how an agent reads when it fits: what it is for and how to do it.",
            Schema(
                ("id", "The skill's folder name: lowercase letters, digits and dashes, like laravel-migrations.", true),
                ("name", "A short name people read, like Laravel migrations.", true),
                ("description", "One or two sentences on what the skill does; agents see this to decide whether to read it.", true),
                ("whenToUse", "When an agent should reach for it, like Changing the database schema.", false),
                ("instructions", "The skill itself, in Markdown: steps, conventions, examples.", true)),
            ReadOnly: false,
            call => Run(() => abilities.SaveSkill(new SkillDraft(
                call.Text("id") ?? string.Empty,
                call.Text("name") ?? call.Text("id") ?? string.Empty,
                call.Text("description") ?? string.Empty,
                call.Text("whenToUse"),
                call.Text("instructions") ?? string.Empty)))),
    ];

    private static Task<McpResult> Run(Func<Told> act) => Run(() => Task.FromResult(act()));

    private static async Task<McpResult> Run(Func<Task<Told>> act)
    {
        var told = await Dispatcher.UIThread.InvokeAsync(act);

        return new McpResult(told.Text, told.IsError);
    }

    private static T OnUi<T>(Func<T> read) => Dispatcher.UIThread.CheckAccess() ? read() : Dispatcher.UIThread.Invoke(read);

    private static JsonObject Schema(params (string Name, string Description, bool Required)[] fields) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(fields.Select(field => KeyValuePair.Create(
            field.Name,
            (JsonNode?)new JsonObject { ["type"] = "string", ["description"] = field.Description }))),
        ["required"] = new JsonArray([.. fields.Where(field => field.Required).Select(field => (JsonNode)field.Name)]),
    };
}
