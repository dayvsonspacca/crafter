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

    private const string Introduction = """{"type":"control_request","request_id":"crafter","request":{"subtype":"initialize"}}""";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly Entry command = new("executable", "Executable", "The command that starts Claude Code.", executable);

    private readonly ClaudeCodeHistory history = new(home ?? ClaudeCodeHistory.Home);

    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [ClaudeCodeOptions.Edits] = "Edit files",
        ["plan"] = "Plan",
        ["auto"] = "Auto",
        ["dontAsk"] = "Don't ask",
        ["bypassPermissions"] = "Bypass permissions",
        ["manual"] = "Manual",
        ["xhigh"] = "Extra high",
    };

    private ClaudeCodeCapabilities capabilities = ClaudeCodeCapabilities.Unknown;

    public AgentId Id { get; } = new("claude-code");

    public string Name => "Claude Code";

    public string Summary => "Anthropic's coding agent";

    public IReadOnlyList<Setting> Settings(Configuration configuration) =>
    [
        new Choice("model", "Model", "The model that answers.", [new(null, "Default"), .. Offered(configuration, "model", capabilities.Models)]),
        new Choice("effort", "Effort", "How much the model thinks before answering.", [new(null, "Default"), .. Offered(configuration, "effort", capabilities.Efforts)]),
        new Choice(
            "tools",
            "Tools",
            "What Claude Code can do in the project.",
            [new(null, Names[ClaudeCodeOptions.Edits]), new(ClaudeCodeOptions.Chats, "Chat only"), .. Offered(configuration, "tools", capabilities.Modes, ClaudeCodeOptions.Edits, ClaudeCodeOptions.Chats, "edit")]),
        command,
    ];

    public IAgentSession Begin(Project project, string? resumeFrom = null) =>
        new ClaudeCodeSession(project.Folder, Options, resumeFrom);

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
            await process.StandardInput.WriteLineAsync(Introduction);
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

    private static string Named(string value) =>
        Names.TryGetValue(value, out var name) ? name : Humanize(value);

    private static string Humanize(string value)
    {
        var words = string.Concat(value.Select((letter, at) => at > 0 && char.IsUpper(letter) ? " " + char.ToLowerInvariant(letter) : letter.ToString()))
            .Replace('-', ' ')
            .Replace('_', ' ');

        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private ClaudeCodeOptions Options(Configuration chosen) =>
        new(command.Value(chosen), chosen["model"], chosen["effort"], chosen["tools"]);

    private static IEnumerable<Option> Offered(Configuration configuration, string key, IEnumerable<ClaudeCodeModel> found)
    {
        List<ClaudeCodeModel> models = [.. found];
        if (configuration[key] is { } chosen && models.All(model => model.Value != chosen))
        {
            models.Add(new ClaudeCodeModel(chosen));
        }

        return models.Select(model => new Option(model.Value, model.Name ?? Named(model.Value)));
    }

    private static IEnumerable<Option> Offered(Configuration configuration, string key, IEnumerable<string> found, params string[] already)
    {
        List<string> values = [.. found.Except(already)];
        if (configuration[key] is { } chosen && !values.Contains(chosen) && !already.Contains(chosen))
        {
            values.Add(chosen);
        }

        return values.Select(value => new Option(value, Named(value)));
    }

    private sealed record Answer(int Code, string Output, string Complaints);
}
