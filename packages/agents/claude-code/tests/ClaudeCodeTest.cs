using Crafter.Core.Agents;
using Crafter.Core.Projects;

namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class ClaudeCodeTest
{
    private const string Version = "echo '2.1.317 (Claude Code)'";

    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public void IsKnownAsClaudeCode()
    {
        var agent = new ClaudeCode();

        Assert.Equal(new AgentId("claude-code"), agent.Id);
        Assert.Equal("Claude Code", agent.Name);
        Assert.Contains("claude command", agent.Summary);
    }

    [Fact]
    public async Task IsReadyWhenInstalledAndSignedIn()
    {
        using var claude = new FakeClaude(Script("""echo '{"loggedIn":true,"authMethod":"claude.ai","subscriptionType":"team","email":"hero@example.com"}'"""));

        Assert.Equal(new Ready("2.1.317", "claude.ai · team"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsReadyWithoutAnAccountItCannotRead()
    {
        using var claude = new FakeClaude(Script("echo 'not json'"));

        Assert.Equal(new Ready("2.1.317", null), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsReadyWhenTheAccountSaysNothingAboutItself()
    {
        using var claude = new FakeClaude(Script("echo '{\"loggedIn\":true}'"));

        Assert.Equal(new Ready("2.1.317", null), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsReadyWhenItCannotTellTheAuthStatus()
    {
        using var claude = new FakeClaude(Script("echo 'unknown command' >&2; exit 1"));

        Assert.Equal(new Ready("2.1.317", null), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task AsksToSignInWhenSignedOut()
    {
        using var claude = new FakeClaude(Script("""echo '{"loggedIn":false,"authMethod":"none"}'"""));

        Assert.Equal(new SignedOut("claude auth login"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task AsksToInstallWhenMissing()
    {
        var readiness = await new ClaudeCode("/nonexistent/crafter-claude").Check(Stop);

        Assert.Equal(new Missing(ClaudeCode.Install), readiness);
    }

    [Fact]
    public async Task IsBrokenWithWhatItComplained()
    {
        using var claude = new FakeClaude("echo 'libc mismatch' >&2\nexit 2");

        Assert.Equal(new Broken("libc mismatch"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsBrokenWhenTheVersionIsNotAVersion()
    {
        using var claude = new FakeClaude("echo '{\"type\":\"system\"}'");

        Assert.Equal(new Broken("`claude --version` did not answer with a version."), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsBrokenWithTheExitCodeWhenSilent()
    {
        using var claude = new FakeClaude("exit 2");

        Assert.Equal(new Broken("`claude --version` exited with code 2."), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task BeginsASessionInTheProjectFolder()
    {
        using var claude = new FakeClaude($"cat > /dev/null\n{FakeClaude.Writes("$(pwd)")}\n{FakeClaude.Success}");
        var session = new ClaudeCode(claude.Executable).Begin(new Project(claude.Folder), resumeFrom: "s-0");

        var replies = await session.Answer(new Crafter.Core.Conversations.Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Contains(new Crafter.Core.Conversations.Wrote(claude.Folder), replies);
        Assert.Equal("s-1", session.SessionId);
    }

    private static string Script(string status) =>
        $"""
        if [[ "$1" == "--version" ]]; then {Version}; exit 0; fi
        if [[ "$1" == "auth" ]]; then {status}; fi
        """;
}
