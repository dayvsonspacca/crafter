using System.Text.Json;
using Crafter.Agents.ClaudeCode.Cli;
using Crafter.Agents.ClaudeCode.History;
using Crafter.Agents.ClaudeCode.Protocol;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Library;
using Crafter.Core.Projects;
using static Crafter.Agents.ClaudeCode.Protocol.JsonFields;

namespace Crafter.Agents.ClaudeCode;

// Servers says which tool servers a project's sessions connect to and skills which library skills
// they get; none unless Crafter grants them. Shelves is where the skills are copied for Claude Code.
public sealed class ClaudeCode(
    string executable = "claude",
    string? home = null,
    Func<Project, IReadOnlyList<ToolServer>>? servers = null,
    Func<Project, IReadOnlyList<Skill>>? skills = null,
    string? shelves = null) : IAgent, IKeepsHistory
{
    public const string Install = "curl -fsSL https://claude.ai/install.sh | bash";

    public const string SignIn = "claude auth login";

    private const string Broken = "Not working";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly Entry command = new(ClaudeCodeSettings.Executable, "Executable", "The command that starts Claude Code.", executable);

    private readonly ClaudeCodeHistory history = new(home ?? ClaudeCodeHistory.Home);

    private ClaudeCodeCapabilities capabilities = ClaudeCodeCapabilities.Unknown;

    public AgentId Id { get; } = new("claude-code");

    public string Name => "Claude Code";

    public string Summary => "Anthropic's coding agent";

    public IReadOnlyList<Setting> Settings(Configuration configuration) =>
    [
        new Choice(
            ClaudeCodeSettings.Model,
            "Model",
            "The model that answers.",
            [new(null, "Default"), .. Offered(configuration, ClaudeCodeSettings.Model, capabilities.Models)]),
        new Choice(
            ClaudeCodeSettings.Effort,
            "Effort",
            "How much the model thinks before answering.",
            [new(null, "Default"), .. Offered(configuration, ClaudeCodeSettings.Effort, capabilities.Efforts)]),
        new Choice(
            ClaudeCodeSettings.Tools,
            "Tools",
            "What Claude Code can do in the project.",
            [
                new(null, ClaudeCodeLabels.Of(ClaudeCodeOptions.Edits)),
                new(ClaudeCodeOptions.Chats, "Chat only"),
                .. Offered(configuration, ClaudeCodeSettings.Tools, capabilities.Modes, ClaudeCodeOptions.Edits, ClaudeCodeOptions.Chats, ClaudeCodeOptions.Legacy),
            ]),
        command,
    ];

    public IAgentSession Begin(Project project, string? resumeFrom = null) =>
        new ClaudeCodeSession(project.Folder, Options, resumeFrom, () => servers?.Invoke(project) ?? [], () => Skills(project));

    public Task<Recollection> History(Project project, int latest, CancellationToken stop = default) => history.List(project, latest, stop);

    public Task<IReadOnlyList<Turn>> Replay(Project project, string sessionId, CancellationToken stop = default) =>
        history.Replay(project, sessionId, stop);

    public async Task<Readiness> Check(Configuration configuration, CancellationToken stop = default)
    {
        var executable = Options(configuration).Executable;
        var version = await Ask(executable, ["--version"], stop);
        if (version is null)
        {
            return new Blocked("Not installed", "Install it from a terminal.", Install);
        }

        if (version.Code != 0)
        {
            return new Blocked(Broken, Said(version, $"`claude --version` exited with code {version.Code}."), null);
        }

        var installed = version.Output.Trim().Split(' ', 2)[0];
        if (installed.Length == 0 || !char.IsAsciiDigit(installed[0]))
        {
            return new Blocked(Broken, "`claude --version` did not answer with a version.", null);
        }

        if (await Ask(executable, ["--help"], stop) is { Code: 0 } help)
        {
            capabilities = ClaudeCodeCapabilities.Parse(help.Output).Introduced(await Introduce(executable, stop));
        }

        var status = await Ask(executable, ["auth", "status", "--json"], stop);

        if (status is null || status.Code != 0)
        {
            return new Ready("Ready", $"Version {installed}");
        }

        return Account(status.Output) switch
        {
            { LoggedIn: false } => new Blocked("Signed out", "Sign in from a terminal.", SignIn),
            { Label: { } label } => new Ready("Ready", $"Version {installed} · {label}"),
            _ => new Ready("Ready", $"Version {installed}"),
        };
    }

    private static async Task<Answer?> Ask(string executable, string[] arguments, CancellationToken stop)
    {
        using var process = ClaudeCodeProcess.Start(executable, Environment.CurrentDirectory, arguments);
        if (process is null)
        {
            return null;
        }

        process.StandardInput.Close();
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(stop);
        patience.CancelAfter(Patience);

        try
        {
            var output = process.StandardOutput.ReadToEndAsync(patience.Token);
            var complaints = process.StandardError.ReadToEndAsync(patience.Token);
            await process.WaitForExitAsync(patience.Token);

            return new Answer(process.ExitCode, await output, await complaints);
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            ClaudeCodeProcess.Kill(process);

            return new Answer(-1, string.Empty, $"`claude {string.Join(' ', arguments)}` did not answer in {Patience.TotalSeconds:0} seconds.");
        }
    }

    private static async Task<string?> Introduce(string executable, CancellationToken stop)
    {
        using var process = ClaudeCodeProcess.Start(
            executable,
            Environment.CurrentDirectory,
            ["-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose"]);
        if (process is null)
        {
            return null;
        }

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(stop);
        patience.CancelAfter(Patience);

        try
        {
            await process.StandardInput.WriteLineAsync(ClaudeCodeProtocol.Introduction);
            await process.StandardInput.FlushAsync(patience.Token);

            while (await process.StandardOutput.ReadLineAsync(patience.Token) is { } line)
            {
                if (line.Contains("\"control_response\"", StringComparison.Ordinal))
                {
                    return line;
                }
            }

            return null;
        }
        catch (Exception unanswered) when (unanswered is OperationCanceledException or IOException)
        {
            return null;
        }
        finally
        {
            ClaudeCodeProcess.Kill(process);
        }
    }

    private static (bool LoggedIn, string? Label) Account(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var loggedIn = !root.TryGetProperty("loggedIn", out var flag) || flag.ValueKind != JsonValueKind.False;
            string[] parts = [.. new[] { Text(root, "authMethod"), Text(root, "subscriptionType") }.OfType<string>().Where(part => part.Length > 0)];

            return (loggedIn, parts.Length > 0 ? string.Join(" · ", parts) : null);
        }
        catch (JsonException)
        {
            return (true, null);
        }
    }

    private static string Said(Answer answer, string otherwise) =>
        answer.Complaints.Trim() is { Length: > 0 } complaint ? complaint : otherwise;

    // Skills that cannot be copied leave the session without them rather than without a start.
    private ClaudeCodeSkillShelf? Skills(Project project)
    {
        if (skills is null || shelves is null)
        {
            return null;
        }

        try
        {
            return ClaudeCodeSkills.Write(ClaudeCodeSkills.FolderFor(shelves, project.Folder), skills(project));
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private ClaudeCodeOptions Options(Configuration chosen) =>
        new(command.Value(chosen), chosen[ClaudeCodeSettings.Model], chosen[ClaudeCodeSettings.Effort], chosen[ClaudeCodeSettings.Tools]);

    private static IEnumerable<Option> Offered(Configuration configuration, string key, IEnumerable<ClaudeCodeModel> found)
    {
        List<ClaudeCodeModel> models = [.. found];
        if (configuration[key] is { } chosen && models.All(model => model.Value != chosen))
        {
            models.Add(new ClaudeCodeModel(chosen));
        }

        return models.Select(model => new Option(model.Value, model.Name ?? ClaudeCodeLabels.Of(model.Value)));
    }

    private static IEnumerable<Option> Offered(Configuration configuration, string key, IEnumerable<string> found, params string[] already)
    {
        List<string> values = [.. found.Except(already)];
        if (configuration[key] is { } chosen && !values.Contains(chosen) && !already.Contains(chosen))
        {
            values.Add(chosen);
        }

        return values.Select(value => new Option(value, ClaudeCodeLabels.Of(value)));
    }

    private sealed record Answer(int Code, string Output, string Complaints);
}
