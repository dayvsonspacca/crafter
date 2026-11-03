using System.Text.Json;
using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Agents.ClaudeCode;

public sealed class ClaudeCode(string executable = "claude", string? home = null) : IAgent, IKeepsHistory
{
    public const string Install = "curl -fsSL https://claude.ai/install.sh | bash";

    public const string SignIn = "claude auth login";

    private const string Broken = "Not working";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly Entry command = new("executable", "Executable", "The command that starts Claude Code.", executable);

    private readonly ClaudeCodeHistory history = new(home ?? ClaudeCodeHistory.Home);

    private readonly Choice model = new(
        "model",
        "Model",
        "The model that answers.",
        [new(null, "Default"), new("fable", "Fable"), new("opus", "Opus"), new("sonnet", "Sonnet"), new("haiku", "Haiku")]);

    private readonly Choice effort = new(
        "effort",
        "Effort",
        "How much the model thinks before answering.",
        [new(null, "Default"), new("low", "Low"), new("medium", "Medium"), new("high", "High"), new("max", "Max")]);

    private readonly Choice tools = new(
        "tools",
        "Tools",
        "What Claude Code can do in the project.",
        [new(null, "Chat only"), new(ClaudeCodeOptions.Plans, "Plan"), new(ClaudeCodeOptions.Edits, "Edit files")]);

    private ClaudeCodeOptions options = new(executable);

    private Configuration configuration = Configuration.Empty;

    public AgentId Id { get; } = new("claude-code");

    public string Name => "Claude Code";

    public string Summary => "Anthropic's coding agent";

    public IReadOnlyList<Setting> Settings => [model, effort, tools, command];

    public IReadOnlyList<string> Badges =>
        [.. Made(model, label => label), .. Made(effort, label => $"{label} effort"), tools.Chosen(configuration).Label];

    public void Configure(Configuration configuration)
    {
        this.configuration = configuration;
        options = new ClaudeCodeOptions(command.Value(configuration), configuration["model"], configuration["effort"], configuration["tools"]);
    }

    public IAgentSession Begin(Project project, string? resumeFrom = null) =>
        new ClaudeCodeSession(project.Folder, () => options, resumeFrom);

    public Task<Recollection> History(Project project, int latest, CancellationToken stop = default) => history.List(project, latest, stop);

    public Task<IReadOnlyList<Turn>> Replay(Project project, string sessionId, CancellationToken stop = default) =>
        history.Replay(project, sessionId, stop);

    public async Task<Readiness> Check(CancellationToken stop = default)
    {
        var version = await Ask(["--version"], stop);
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

        var status = await Ask(["auth", "status", "--json"], stop);

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

    private async Task<Answer?> Ask(string[] arguments, CancellationToken stop)
    {
        using var process = ClaudeCodeProcess.Start(options.Executable, Environment.CurrentDirectory, arguments);
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

    private static (bool LoggedIn, string? Label) Account(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var loggedIn = !root.TryGetProperty("loggedIn", out var flag) || flag.ValueKind != JsonValueKind.False;
            string[] parts = [.. new[] { Text(root, "authMethod"), Text(root, "subscriptionType") }.OfType<string>()];

            return (loggedIn, parts.Length > 0 ? string.Join(" · ", parts) : null);
        }
        catch (JsonException)
        {
            return (true, null);
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static string Said(Answer answer, string otherwise) =>
        answer.Complaints.Trim() is { Length: > 0 } complaint ? complaint : otherwise;

    private IEnumerable<string> Made(Choice choice, Func<string, string> badge) =>
        choice.Chosen(configuration) is { Value: not null } option ? [badge(option.Label)] : [];

    private sealed record Answer(int Code, string Output, string Complaints);
}
