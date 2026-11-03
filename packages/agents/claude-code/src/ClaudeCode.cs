using System.Text.Json;
using Crafter.Core.Agents;
using Crafter.Core.Projects;

namespace Crafter.Agents.ClaudeCode;

public sealed class ClaudeCode(string executable = "claude") : IAgent
{
    public const string Install = "curl -fsSL https://claude.ai/install.sh | bash";

    public const string SignIn = "claude auth login";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    public AgentId Id { get; } = new("claude-code");

    public string Name => "Claude Code";

    public string Summary => "Anthropic's coding agent, run through the claude command.";

    public IAgentSession Begin(Project project, string? resumeFrom = null) =>
        new ClaudeCodeSession(project.Folder, executable, resumeFrom);

    public async Task<Readiness> Check(CancellationToken stop = default)
    {
        var version = await Ask(["--version"], stop);
        if (version is null)
        {
            return new Missing(Install);
        }

        if (version.Code != 0)
        {
            return new Broken(Said(version, $"`claude --version` exited with code {version.Code}."));
        }

        var installed = version.Output.Trim().Split(' ', 2)[0];
        if (installed.Length == 0 || !char.IsAsciiDigit(installed[0]))
        {
            return new Broken("`claude --version` did not answer with a version.");
        }

        var status = await Ask(["auth", "status", "--json"], stop);

        if (status is null || status.Code != 0)
        {
            return new Ready(installed, null);
        }

        return Account(status.Output) switch
        {
            { LoggedIn: false } => new SignedOut(SignIn),
            { Label: var label } => new Ready(installed, label),
        };
    }

    private async Task<Answer?> Ask(string[] arguments, CancellationToken stop)
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

    private sealed record Answer(int Code, string Output, string Complaints);
}
