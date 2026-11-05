using System.Text.Json;
using System.Text.Json.Nodes;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using Crafter.Presentation.Folders;
using Crafter.Presentation.Library;

namespace Crafter.Presentation.Abilities;

// What Home's agent can do to Crafter: add, create, open and remove projects, rename them, change
// their icons, set their axioms, summaries and todos, and read and write the skills library. A project's
// agent can only propose axioms and a summary, and keep the todos, for its own project. Answers are JSON for the agent to read.
// A relative folder is read from the projects root set in Preferences.
public sealed class CrafterAbilities(WorkbenchViewModel workbench, LibraryViewModel library, IBranches branches)
{
    private static readonly JsonSerializerOptions Readable = new() { WriteIndented = true };

    public Told ListProjects() => Json(new JsonArray([.. workbench.Projects.Select(project => (JsonNode)new JsonObject
    {
        ["name"] = project.Name.Text,
        ["folder"] = project.Folder,
        ["branch"] = branches.Of(project),
        ["openChats"] = workbench.OpenChats(project),
        ["summary"] = workbench.SummaryOf(project),
        ["axioms"] = new JsonArray([.. workbench.AxiomsOf(project).Select(axiom => (JsonNode)axiom)]),
    })]));

    // Home names the project; a project's agent proposes for its own, and only for it. The user
    // approves every axiom, whatever the chat's mode: approve asks them in the chat that proposed it.
    public async Task<Told> ProposeAxiom(string caller, string? folder, string? axiom, Func<Project, string, Task<bool>> approve)
    {
        if (Proposer(caller, folder) is not { } project)
        {
            return Refusal(caller, folder, "A project's agent proposes axioms for its own project only.");
        }

        if (string.IsNullOrWhiteSpace(axiom))
        {
            return Told.Failed("An axiom needs its text.");
        }

        var text = axiom.Trim();
        if (workbench.AxiomsOf(project).Contains(text))
        {
            return new Told($"{project.Name.Text} already holds that axiom.");
        }

        if (!await approve(project, text))
        {
            return new Told($"The user declined the axiom \"{text}\"; {project.Name.Text} does not hold it.");
        }

        workbench.Hold(project, text);

        return new Told($"{project.Name.Text} now holds the axiom \"{text}\"; its chats follow it from their next session.");
    }

    // The whole summary, never a piece of it, so what the user approves is what the agent reads.
    // Approve gets the summary it would replace, to show the change.
    public async Task<Told> ProposeSummary(string caller, string? folder, string? summary, Func<Project, string?, string, Task<bool>> approve)
    {
        if (Proposer(caller, folder) is not { } project)
        {
            return Refusal(caller, folder, "A project's agent proposes a summary for its own project only.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return Told.Failed("A summary needs its text.");
        }

        var text = summary.Trim();
        var current = workbench.SummaryOf(project);
        if (text == current)
        {
            return new Told($"{project.Name.Text} already has that summary.");
        }

        if (!await approve(project, current, text))
        {
            return new Told($"The user declined the summary; {project.Name.Text} keeps {(current is null ? "none" : "the one it had")}.");
        }

        workbench.Summarize(project, text);

        return new Told($"{project.Name.Text} now has the summary; its chats read it from their next session.");
    }

    public Told ListTodos(string caller, string? folder)
    {
        if (Proposer(caller, folder) is not { } project)
        {
            return Refusal(caller, folder, "A project's agent keeps the todos of its own project only.");
        }

        return Json(new JsonArray([.. workbench.TodosOf(project).Select(todo => (JsonNode)new JsonObject
        {
            ["number"] = todo.Number,
            ["text"] = todo.Text,
            ["done"] = todo.IsDone,
        })]));
    }

    public Told AddTodo(string caller, string? folder, string? text)
    {
        if (Proposer(caller, folder) is not { } project)
        {
            return Refusal(caller, folder, "A project's agent keeps the todos of its own project only.");
        }

        return !string.IsNullOrWhiteSpace(text) && workbench.Jot(project, text) is { } jotted
            ? new Told($"{project.Name.Text} now has todo #{jotted.Number}: {jotted.Text}")
            : Told.Failed("A todo needs its text.");
    }

    public Told CompleteTodo(string caller, string? folder, int? number)
    {
        if (Proposer(caller, folder) is not { } project)
        {
            return Refusal(caller, folder, "A project's agent keeps the todos of its own project only.");
        }

        if (workbench.TodosOf(project).FirstOrDefault(todo => todo.Number == number) is not { } todo)
        {
            return Told.Failed($"{project.Name.Text} has no todo #{number}; list_todos gives the numbers.");
        }

        return workbench.Tick(project, todo.Number, done: true)
            ? new Told($"Todo #{todo.Number} of {project.Name.Text} is done: {todo.Text}")
            : new Told($"Todo #{todo.Number} of {project.Name.Text} was already done.");
    }

    public Told RemoveTodo(string caller, string? folder, int? number)
    {
        if (Proposer(caller, folder) is not { } project)
        {
            return Refusal(caller, folder, "A project's agent keeps the todos of its own project only.");
        }

        if (workbench.TodosOf(project).FirstOrDefault(todo => todo.Number == number) is not { } todo)
        {
            return Told.Failed($"{project.Name.Text} has no todo #{number}; list_todos gives the numbers.");
        }

        workbench.Scrap(project, todo.Number);

        return new Told($"Removed todo #{todo.Number} from {project.Name.Text}: {todo.Text}");
    }

    public Told RemoveAxiom(string? folder, string? axiom)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        return !string.IsNullOrWhiteSpace(axiom) && workbench.Drop(project, axiom)
            ? new Told($"{project.Name.Text} no longer holds the axiom \"{axiom.Trim()}\".")
            : Told.Failed($"{project.Name.Text} holds no axiom \"{axiom}\"; list_projects gives its axioms.");
    }

    public Told AddProject(string? folder, string? name)
    {
        if (Under(folder) is not { } full)
        {
            return Told.Failed("A project needs a folder.");
        }

        if (!Directory.Exists(full))
        {
            return Told.Failed($"There is no folder at {full}; create_project makes a new one.");
        }

        if (Refused(full) is { } refused)
        {
            return refused;
        }

        var added = workbench.Shelve(full, string.IsNullOrWhiteSpace(name) ? workbench.NameOf(full) : new ProjectName(name));

        return new Told($"Added {added.Name.Text} to Crafter, from {added.Folder}.");
    }

    // A new, empty folder; without one given it is the name, in lowercase and dashes, under the
    // projects root. Git and anything else in it are the chat's to do.
    public Told CreateProject(string? name, string? folder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Told.Failed("A project needs a name.");
        }

        var full = Under(folder) ?? Path.Combine(workbench.ProjectsRoot, Slug(name));
        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
        {
            return Told.Failed($"{full} already holds files; add_project adds a folder that exists.");
        }

        if (Refused(full) is { } refused)
        {
            return refused;
        }

        try
        {
            Directory.CreateDirectory(full);
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            return Told.Failed($"Could not create {full}: {unwritable.Message}");
        }

        var created = workbench.Shelve(full, new ProjectName(name));

        return new Told($"Created {created.Folder} and added {created.Name.Text} to Crafter.");
    }

    public Told OpenProject(string? folder)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        workbench.Open(project);

        return new Told($"Opened {project.Name.Text}.");
    }

    // Takes it off the sidebar and ends its open chats; the folder stays as it is.
    public Told RemoveProject(string? folder)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        workbench.Remove(project);

        return new Told($"Removed {project.Name.Text} from Crafter; its folder stays at {project.Folder}.");
    }

    public Told RenameProject(string? folder, string? name)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Told.Failed("A project needs a name.");
        }

        var before = project.Name.Text;
        var renamed = workbench.Rename(project, new ProjectName(name));

        return new Told($"Renamed {before} to {renamed.Name.Text}.");
    }

    // A relative image is read from the folder of the asking agent, where it works.
    public Told SetProjectIcon(string caller, string? folder, string? image)
    {
        if (Find(folder) is not { } project)
        {
            return Told.Failed($"No project lives in {folder}; list_projects gives the folders.");
        }

        if (string.IsNullOrWhiteSpace(image))
        {
            return Told.Failed("An icon needs an image file.");
        }

        var file = Path.GetFullPath(image, caller);
        if (!File.Exists(file))
        {
            return Told.Failed($"There is no file at {file}.");
        }

        if (!ImageEntryViewModel.Formats.Contains(Path.GetExtension(file)))
        {
            return Told.Failed($"Crafter shows {string.Join(", ", ImageEntryViewModel.Formats)} icons; {Path.GetFileName(file)} is none of them.");
        }

        try
        {
            var changed = workbench.ChangeIcon(project, file);

            return new Told($"{changed.Name.Text} now shows {Path.GetFileName(file)} as its icon, kept in {changed.Icon}.");
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            return Told.Failed($"Could not keep the icon in {project.Name.Text}: {unwritable.Message}");
        }
    }

    public Told LibraryStatus() => Json(new JsonObject
    {
        ["repository"] = library.Repository,
        ["status"] = library.Status,
        ["skills"] = library.Skills.Count,
        ["problems"] = new JsonArray([.. library.Problems.Select(problem => (JsonNode)$"{problem.Place}: {problem.Reason}")]),
    });

    public async Task<Told> SyncLibrary()
    {
        if (!library.IsLinked)
        {
            return Told.Failed(Unlinked);
        }

        await library.SyncCommand.ExecuteAsync(null);

        return library.HasFailed ? Told.Failed(library.Status) : new Told(library.Status);
    }

    public Told ListSkills() => Json(new JsonArray([.. library.Skills.Select(skill => (JsonNode)new JsonObject
    {
        ["id"] = skill.Id,
        ["name"] = skill.Name,
        ["description"] = skill.Description,
        ["whenToUse"] = skill.WhenToUse,
    })]));

    public Told ReadSkill(string? id)
    {
        if (library.Skills.FirstOrDefault(skill => skill.Id == id) is not { } skill)
        {
            return Told.Failed($"The library has no skill {id}; list_skills gives the ids.");
        }

        return Json(new JsonObject
        {
            ["id"] = skill.Id,
            ["name"] = skill.Name,
            ["description"] = skill.Description,
            ["whenToUse"] = skill.WhenToUse,
            ["instructions"] = File.ReadAllText(skill.Instructions),
        });
    }

    public async Task<Told> SaveSkill(SkillDraft skill)
    {
        return await library.Save(skill) switch
        {
            Synced synced => new Told($"Saved {skill.Id} to the library and pushed it ({synced.Commit})."),
            SyncFailed failed => Told.Failed($"Could not save {skill.Id}: {failed.Reason}"),
            _ => Told.Failed($"Could not save {skill.Id}."),
        };
    }

    private const string Unlinked = "No library repository is linked; link one in Preferences → Library.";

    // Home names the project; a project's agent proposes and keeps todos for its own, and only for it.
    private Project? Proposer(string caller, string? folder)
    {
        if (caller == workbench.Home.Folder)
        {
            return Find(folder);
        }

        var own = workbench.Projects.FirstOrDefault(each => each.Folder == caller);

        return own is not null && (Under(folder) is not { } named || named == own.Folder) ? own : null;
    }

    private Told Refusal(string caller, string? folder, string elsewhere) =>
        caller == workbench.Home.Folder ? Told.Failed($"No project lives in {folder}; list_projects gives the folders.")
        : workbench.Projects.Any(each => each.Folder == caller) ? Told.Failed(elsewhere)
        : Told.Failed("Your project is no longer in Crafter.");

    private Project? Find(string? folder) =>
        Under(folder) is { } full ? workbench.Projects.FirstOrDefault(project => project.Folder == Path.TrimEndingDirectorySeparator(full)) : null;

    private string? Under(string? folder) =>
        string.IsNullOrWhiteSpace(folder) ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Trim(), workbench.ProjectsRoot));

    private Told? Refused(string folder)
    {
        if (folder == workbench.Home.Folder)
        {
            return Told.Failed("That folder is Home's own.");
        }

        return workbench.Projects.FirstOrDefault(project => project.Folder == folder) is { } shelved
            ? Told.Failed($"{shelved.Name.Text} is already in Crafter, from {folder}.")
            : null;
    }

    // "Lorekeeper Desktop" goes in lorekeeper-desktop.
    private static string Slug(string name)
    {
        var words = new string([.. name.Trim().ToLowerInvariant().Select(letter => char.IsLetterOrDigit(letter) ? letter : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length > 0 ? string.Join('-', words) : "project";
    }

    private static Told Json(JsonNode answer) => new(answer.ToJsonString(Readable));
}

public sealed record Told(string Text, bool IsError = false)
{
    public static Told Failed(string reason) => new(reason, IsError: true);
}
