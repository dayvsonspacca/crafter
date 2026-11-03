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
        Assert.Equal("Anthropic's coding agent", agent.Summary);
    }

    [Fact]
    public async Task IsReadyWhenInstalledAndSignedIn()
    {
        using var claude = new FakeClaude(Script("""echo '{"loggedIn":true,"authMethod":"claude.ai","subscriptionType":"team","email":"hero@example.com"}'"""));

        Assert.Equal(new Ready("Ready", "Version 2.1.317 · claude.ai · team"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsReadyWithoutAnAccountItCannotRead()
    {
        using var claude = new FakeClaude(Script("echo 'not json'"));

        Assert.Equal(new Ready("Ready", "Version 2.1.317"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsReadyWhenTheAccountSaysNothingAboutItself()
    {
        using var claude = new FakeClaude(Script("echo '{\"loggedIn\":true}'"));

        Assert.Equal(new Ready("Ready", "Version 2.1.317"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsReadyWhenItCannotTellTheAuthStatus()
    {
        using var claude = new FakeClaude(Script("echo 'unknown command' >&2; exit 1"));

        Assert.Equal(new Ready("Ready", "Version 2.1.317"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task AsksToSignInWhenSignedOut()
    {
        using var claude = new FakeClaude(Script("""echo '{"loggedIn":false,"authMethod":"none"}'"""));

        Assert.Equal(new Blocked("Signed out", "Sign in from a terminal.", "claude auth login"), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task AsksToInstallWhenMissing()
    {
        var readiness = await new ClaudeCode("/nonexistent/crafter-claude").Check(Stop);

        Assert.Equal(new Blocked("Not installed", "Install it from a terminal.", ClaudeCode.Install), readiness);
    }

    [Fact]
    public async Task IsBrokenWithWhatItComplained()
    {
        using var claude = new FakeClaude("echo 'libc mismatch' >&2\nexit 2");

        Assert.Equal(new Blocked("Not working", "libc mismatch", null), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsBrokenWhenTheVersionIsNotAVersion()
    {
        using var claude = new FakeClaude("echo '{\"type\":\"system\"}'");

        Assert.Equal(new Blocked("Not working", "`claude --version` did not answer with a version.", null), await new ClaudeCode(claude.Executable).Check(Stop));
    }

    [Fact]
    public async Task IsBrokenWithTheExitCodeWhenSilent()
    {
        using var claude = new FakeClaude("exit 2");

        Assert.Equal(new Blocked("Not working", "`claude --version` exited with code 2.", null), await new ClaudeCode(claude.Executable).Check(Stop));
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

    [Fact]
    public void OffersModelEffortToolsAndExecutable()
    {
        Assert.Equal(["model", "effort", "tools", "executable"], new ClaudeCode().Settings.Select(setting => setting.Key));
    }

    [Fact]
    public async Task OffersWhatTheInstalledClaudeCodeKnows()
    {
        using var claude = new FakeClaude(
            "if [[ \"$1\" == \"--help\" ]]; then cat \"$(dirname \"$0\")/help.txt\"; exit 0; fi\n"
            + "if [[ \"$*\" == *--input-format* ]]; then read -r line; cat \"$(dirname \"$0\")/introduction.json\"; sleep 5; fi\n"
            + Script("""echo '{"loggedIn":true}'"""));
        File.WriteAllText(Path.Combine(claude.Folder, "help.txt"), ClaudeCodeCapabilitiesTest.Help);
        File.WriteAllText(Path.Combine(claude.Folder, "introduction.json"), ClaudeCodeCapabilitiesTest.Introduction + "\n");
        var agent = new ClaudeCode(claude.Executable);

        await agent.Check(Stop);

        Assert.Equal(["Default", "Opus 5.5", "Sonnet 4.6", "Claude next"], Labels(agent, "model"));
        Assert.Equal(["Default", "Low", "Medium", "High", "Extra high", "Max"], Labels(agent, "effort"));
        Assert.Equal(["Edit files", "Chat only", "Auto", "Bypass permissions", "Manual", "Don't ask", "Plan"], Labels(agent, "tools"));
    }

    [Fact]
    public async Task FallsBackToTheAliasesWhenClaudeCodeDoesNotIntroduceItself()
    {
        using var claude = new FakeClaude(
            "if [[ \"$1\" == \"--help\" ]]; then cat \"$(dirname \"$0\")/help.txt\"; exit 0; fi\n"
            + "if [[ \"$*\" == *--input-format* ]]; then exit 1; fi\n"
            + Script("""echo '{"loggedIn":true}'"""));
        File.WriteAllText(Path.Combine(claude.Folder, "help.txt"), ClaudeCodeCapabilitiesTest.Help);
        var agent = new ClaudeCode(claude.Executable);

        await agent.Check(Stop);

        Assert.Equal(["Default", "Fable", "Opus", "Sonnet"], Labels(agent, "model"));
    }

    [Fact]
    public void KeepsOfferingAChoiceTheHelpNoLongerLists()
    {
        var agent = new ClaudeCode();

        agent.Configure(Configuration.Empty.With("effort", "turbo-mode").With("tools", "edit").With("model", "claude-opus-4-1"));

        Assert.Equal(["Default", "Turbo mode"], Labels(agent, "effort"));
        Assert.Equal(["Default", "Claude opus 4 1"], Labels(agent, "model"));
        Assert.Equal(["Edit files", "Chat only"], Labels(agent, "tools"));
    }

    [Fact]
    public async Task AnswersWithTheAdjustmentsOfTheChatOverThePreferences()
    {
        using var claude = new FakeClaude($"cat > /dev/null\n{FakeClaude.Writes("$*")}\n{FakeClaude.Success}");
        var agent = new ClaudeCode(claude.Executable);
        agent.Configure(Configuration.Empty.With("model", "opus"));
        var session = agent.Begin(new Project(claude.Folder));

        session.Adjust(Configuration.Empty.With("tools", ClaudeCodeOptions.Chats));
        var replies = await session.Answer(new Crafter.Core.Conversations.Prompt("oi"), Stop).ToListAsync(Stop);

        var said = Assert.Single(replies.OfType<Crafter.Core.Conversations.Wrote>()).Fragment;
        Assert.Contains("--model opus", said);
        Assert.Contains("--tools", said);
        Assert.Equal("chat", session.Adjustments["tools"]);
    }

    [Fact]
    public async Task ChecksTheConfiguredExecutable()
    {
        using var claude = new FakeClaude(Script("""echo '{"loggedIn":true}'"""));
        var agent = new ClaudeCode("/nonexistent/crafter-claude");

        agent.Configure(Configuration.Empty.With("executable", claude.Executable));

        Assert.IsType<Ready>(await agent.Check(Stop));
    }

    [Fact]
    public async Task AnswersWithTheConfigurationOfTheMomentItIsAsked()
    {
        using var claude = new FakeClaude($"cat > /dev/null\n{FakeClaude.Writes("$*")}\n{FakeClaude.Success}");
        var agent = new ClaudeCode(claude.Executable);
        var session = agent.Begin(new Project(claude.Folder));

        agent.Configure(Configuration.Empty.With("model", "opus").With("tools", "plan"));
        var replies = await session.Answer(new Crafter.Core.Conversations.Prompt("oi"), Stop).ToListAsync(Stop);

        var said = Assert.Single(replies.OfType<Crafter.Core.Conversations.Wrote>()).Fragment;
        Assert.Contains("--model opus", said);
        Assert.Contains("--permission-mode plan", said);
    }

    private static IEnumerable<string> Labels(ClaudeCode agent, string key) =>
        agent.Settings.OfType<Choice>().Single(choice => choice.Key == key).Options.Select(option => option.Label);

    private static string Script(string status) =>
        $"""
        if [[ "$1" == "--version" ]]; then {Version}; exit 0; fi
        if [[ "$1" == "auth" ]]; then {status}; fi
        """;
}
