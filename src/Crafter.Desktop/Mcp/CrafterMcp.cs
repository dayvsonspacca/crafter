using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Crafter.Core.Agents;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Mcp;
using Crafter.Presentation;
using Crafter.Presentation.Abilities;

namespace Crafter.Desktop.Mcp;

// Crafter as a tool server. Home's agent, the one place Crafter is managed from, gets every tool;
// a project's agent gets only propose_axiom, for its own project. Each session gets a token of its
// own, so a proposal is asked about in the chat it came from. Tools run on the UI thread, so the
// sidebar, the library and the axioms show what they did right away.
public sealed class CrafterMcp : IDisposable
{
    public const string Name = "crafter";

    public const string Instructions =
        "You are Home, the chat the user runs Crafter from. Crafter is the desktop app this session runs in: "
            + "it keeps a list of projects, each a folder with its own chats and agents, and a library of skills in a git repository. "
            + "Each project also holds axioms, the rules its agent follows in every chat. "
            + "When the user asks about their projects, their axioms or the skills library, use these tools; Crafter's own files are not yours to edit.";

    public const string ProjectInstructions =
        "Crafter, the app this session runs in, keeps this project's axioms: the rules its agent follows in every chat, listed in your system prompt. "
            + "When the user corrects you or settles how something must always be done here, propose it as an axiom with propose_axiom, "
            + "short and in the imperative, like \"Never add comments\". The user approves or declines it in the chat. "
            + "Don't propose what is already an axiom, nor what only holds for the task at hand.";

    private const string Proposing = "propose_axiom";

    // Proposing an axiom is free on the agent's side because Crafter asks the user itself, whatever
    // the chat's mode lets the agent do unasked.
    private static readonly string[] Free = ["list_projects", "open_project", "library_status", "sync_library", "list_skills", "read_skill", Proposing];

    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(30);

    private readonly WorkbenchViewModel workbench;

    private readonly CrafterAbilities abilities;

    private readonly ConcurrentDictionary<string, Caller> callers = new();

    private readonly McpServer server;

    public CrafterMcp(WorkbenchViewModel workbench, CrafterAbilities abilities)
    {
        this.workbench = workbench;
        this.abilities = abilities;
        server = new McpServer(Name, "1.0", Tools(), CallerOf, InstructionsFor, Reaches);
    }

    public void Start() => server.Start();

    public IReadOnlyList<ToolServer> ServersFor(Project project, IAgentSession session)
    {
        if (server.Endpoint is not { } endpoint)
        {
            return [];
        }

        if (project == workbench.Home)
        {
            return [new ToolServer(Name, endpoint, TokenFor(project, session), Free, Patience)];
        }

        return OnUi(() => Shelved(project.Folder)) ? [new ToolServer(Name, endpoint, TokenFor(project, session), [Proposing], Patience)] : [];
    }

    public void Dispose() => server.Dispose();

    private string TokenFor(Project project, IAgentSession session)
    {
        foreach (var (issued, caller) in callers)
        {
            if (caller.Session.TryGetTarget(out var known) && known == session)
            {
                return issued;
            }
        }

        var fresh = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        callers[fresh] = new Caller(project.Folder, new WeakReference<IAgentSession>(session));

        return fresh;
    }

    // A project's token stops working once the project leaves the sidebar.
    private string? CallerOf(string offered) =>
        callers.TryGetValue(offered, out var caller) && (caller.Folder == workbench.Home.Folder || OnUi(() => Shelved(caller.Folder)))
            ? caller.Folder
            : null;

    // Asks in the chat whose session carries the token; a chat closed since cannot answer.
    private Func<Project, string, Task<bool>> AskingIn(string token) => (project, axiom) =>
        callers.TryGetValue(token, out var caller) && caller.Session.TryGetTarget(out var session) && workbench.ChatRunning(session) is { } chat
            ? chat.ProposeAxiom(project.Name.Text, axiom)
            : Task.FromResult(false);

    private bool Shelved(string folder) => workbench.Projects.Any(project => project.Folder == folder);

    private bool Reaches(string caller, string tool) => caller == workbench.Home.Folder || tool == Proposing;

    private string InstructionsFor(string caller) => caller == workbench.Home.Folder ? Instructions : ProjectInstructions;

    private McpTool[] Tools() =>
    [
        new(
            "list_projects",
            "Lists the projects in Crafter's sidebar: name, folder, git branch, open chats and axioms.",
            Schema(),
            ReadOnly: true,
            _ => Run(abilities.ListProjects)),
        new(
            "add_project",
            "Adds a folder that already exists on this machine to Crafter's sidebar as a project, without leaving this chat.",
            Schema(
                ("folder", "The folder; a relative path is read from the projects root.", true),
                ("name", "The name to show; without one, the name the folder's .crafter/settings.json keeps, or the folder's own.", false)),
            ReadOnly: false,
            call => Run(() => abilities.AddProject(call.Text("folder"), call.Text("name")))),
        new(
            "create_project",
            "Creates a new, empty folder and adds it to Crafter's sidebar as a project. It does not run git or write any file in it; "
                + "do that afterwards if the user wants it.",
            Schema(
                ("name", "The project's name, as the sidebar shows it.", true),
                ("folder", "Where to create it; a relative path is read from the projects root. Without one, the name in lowercase and dashes under the projects root.", false)),
            ReadOnly: false,
            call => Run(() => abilities.CreateProject(call.Text("name"), call.Text("folder")))),
        new(
            "open_project",
            "Brings a project to the front of Crafter, on its overview or the chat it last showed. This chat keeps going in Home.",
            Schema(("folder", "The project's folder, as list_projects gives it.", true)),
            ReadOnly: false,
            call => Run(() => abilities.OpenProject(call.Text("folder")))),
        new(
            "remove_project",
            "Takes a project off Crafter's sidebar and ends its open chats. Its folder and files stay as they are.",
            Schema(("folder", "The project's folder, as list_projects gives it.", true)),
            ReadOnly: false,
            call => Run(() => abilities.RemoveProject(call.Text("folder")))),
        new(
            "rename_project",
            "Renames a project in Crafter. The name shows in the sidebar and tabs and is kept in the project's .crafter/settings.json.",
            Schema(("folder", "The project's folder, as list_projects gives it.", true), ("name", "The new name.", true)),
            ReadOnly: false,
            call => Run(() => abilities.RenameProject(call.Text("folder"), call.Text("name")))),
        new(
            "set_project_icon",
            "Sets a project's icon in Crafter from an image file on this machine (png, jpg, jpeg or webp; square looks best). "
                + "Crafter copies it into the project's .crafter folder and the sidebar shows it at once.",
            Schema(
                ("folder", "The project's folder, as list_projects gives it.", true),
                ("image", "The image file; a relative path is read from your working folder.", true)),
            ReadOnly: false,
            call => Run(() => abilities.SetProjectIcon(call.Caller, call.Text("folder"), call.Text("image")))),
        new(
            Proposing,
            "Proposes an axiom: a rule a project's agent follows in every chat, short and in the imperative, like \"Never add comments\". "
                + "The user approves it in the chat before Crafter keeps it, and chats follow it from their next session.",
            Schema(
                ("axiom", "The rule, in one sentence.", true),
                ("folder", "Home only: the project's folder, as list_projects gives it. A project's agent leaves it out and proposes for its own project.", false)),
            ReadOnly: false,
            call => Run(() => abilities.ProposeAxiom(call.Caller, call.Text("folder"), call.Text("axiom"), AskingIn(call.Token)))),
        new(
            "remove_axiom",
            "Removes an axiom from a project; its chats stop following it from their next session.",
            Schema(
                ("folder", "The project's folder, as list_projects gives it.", true),
                ("axiom", "The axiom, word for word as list_projects gives it.", true)),
            ReadOnly: false,
            call => Run(() => abilities.RemoveAxiom(call.Text("folder"), call.Text("axiom")))),
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

    private sealed record Caller(string Folder, WeakReference<IAgentSession> Session);

    private static T OnUi<T>(Func<T> read) => Dispatcher.UIThread.CheckAccess() ? read() : Dispatcher.UIThread.Invoke(read);

    private static Task<McpResult> Run(Func<Told> act) => Run(() => Task.FromResult(act()));

    private static async Task<McpResult> Run(Func<Task<Told>> act)
    {
        var told = await Dispatcher.UIThread.InvokeAsync(act);

        return new McpResult(told.Text, told.IsError);
    }

    private static JsonObject Schema(params (string Name, string Description, bool Required)[] fields) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(fields.Select(field => KeyValuePair.Create(
            field.Name,
            (JsonNode?)new JsonObject { ["type"] = "string", ["description"] = field.Description }))),
        ["required"] = new JsonArray([.. fields.Where(field => field.Required).Select(field => (JsonNode)field.Name)]),
    };
}
