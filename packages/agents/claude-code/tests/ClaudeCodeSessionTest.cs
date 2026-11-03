using Crafter.Core.Conversations;

namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class ClaudeCodeSessionTest
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnswersWithWhatClaudeCodeStreams()
    {
        using var claude = new FakeClaude($"read -r line\n{FakeClaude.Init}\n{FakeClaude.Writes("Olá")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal([new Began("Opus 5.5"), new Wrote("Olá"), new Finished()], replies);
    }

    [Fact]
    public async Task SendsThePromptThroughStandardInput()
    {
        using var claude = new FakeClaude(
            $"read -r line\nif [[ \"$line\" == *'\"type\":\"user\"'*'\"content\":\"--help\"'* ]]; then text=heard; else text=missed; fi\n{FakeClaude.Writes("$text")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("--help"), Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("heard"), replies);
    }

    [Fact]
    public async Task ResumesTheSessionOnTheNextQuestion()
    {
        using var claude = new FakeClaude(
            $"""
            read -r line
            if [[ " $* " == *" --resume s-1 "* ]]; then text=resumed; else text=fresh; fi
            {FakeClaude.Init}
            {FakeClaude.Writes("$text")}
            {FakeClaude.Success}
            """);
        var session = claude.Session();

        var first = await session.Answer(new Prompt("oi"), Stop).ToListAsync(Stop);
        var second = await session.Answer(new Prompt("e agora?"), Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("fresh"), first);
        Assert.Contains(new Wrote("resumed"), second);
    }

    [Fact]
    public async Task ResumesASessionItWasGiven()
    {
        using var claude = new FakeClaude(
            $"""
            read -r line
            if [[ " $* " == *" --resume s-0 "* ]]; then text=resumed; else text=fresh; fi
            {FakeClaude.Writes("$text")}
            {FakeClaude.Success}
            """);
        var session = new ClaudeCodeSession(claude.Folder, claude.Executable, resumeFrom: "s-0");

        var replies = await session.Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("resumed"), replies);
    }

    [Fact]
    public async Task KnowsItsSessionOnceClaudeCodeTellsIt()
    {
        using var claude = new FakeClaude($"read -r line\n{FakeClaude.Init}\nsleep 30");
        using var asked = CancellationTokenSource.CreateLinkedTokenSource(Stop);
        var session = claude.Session();

        Assert.Null(session.SessionId);
        await foreach (var reply in session.Answer(new Prompt("oi"), asked.Token))
        {
            if (reply is Began)
            {
                Assert.Equal("s-1", session.SessionId);
                await asked.CancelAsync();
            }
        }

        Assert.Equal("s-1", session.SessionId);
    }

    [Fact]
    public async Task LeavesTheParentClaudeCodeSessionBehind()
    {
        Environment.SetEnvironmentVariable("CLAUDECODE", "1");
        using var claude = new FakeClaude($"read -r line\n{FakeClaude.Writes("${CLAUDECODE:-clean}")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("clean"), replies);
    }

    [Fact]
    public async Task StopsWhenAsked()
    {
        using var claude = new FakeClaude($"read -r line\n{FakeClaude.Init}\nsleep 30");
        using var asked = CancellationTokenSource.CreateLinkedTokenSource(Stop);
        var replies = new List<Reply>();

        await foreach (var reply in claude.Session().Answer(new Prompt("oi"), asked.Token))
        {
            replies.Add(reply);
            if (reply is Began)
            {
                await asked.CancelAsync();
            }
        }

        Assert.Equal([new Began("Opus 5.5"), new Stopped()], replies);
    }

    [Fact]
    public async Task FailsWhenClaudeCodeIsMissing()
    {
        var session = new ClaudeCodeSession(Path.GetTempPath(), "/nonexistent/crafter-claude");

        var replies = await session.Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.IsType<Failed>(Assert.Single(replies));
    }

    [Fact]
    public async Task FailsWithWhatClaudeCodeComplained()
    {
        using var claude = new FakeClaude("read -r line\necho 'Not logged in' >&2\nexit 1");

        var replies = await claude.Session().Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal([new Failed("Not logged in")], replies);
    }

    [Fact]
    public async Task FailsWithTheExitCodeWhenClaudeCodeSaysNothing()
    {
        using var claude = new FakeClaude("read -r line\nexit 3");

        var replies = await claude.Session().Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal([new Failed("Claude Code exited with code 3 without an answer.")], replies);
    }

    [Fact]
    public async Task FailsWhenClaudeCodeLeavesBeforeListening()
    {
        using var claude = new FakeClaude("exit 4");

        var replies = await claude.Session().Answer(new Prompt(new string('x', 1 << 20)), Stop).ToListAsync(Stop);

        Assert.IsType<Failed>(Assert.Single(replies));
    }

    [Theory]
    [InlineData(typeof(Crafter.Core.Agents.Allow), "allowed")]
    [InlineData(typeof(Crafter.Core.Agents.Deny), "denied")]
    public async Task AsksBeforeRunningWhatNeedsApproval(Type decision, string answer)
    {
        using var claude = new FakeClaude("read -r line\n" + Asks("r-1", "first") + "\n" + FakeClaude.Writes("$first") + "\n" + FakeClaude.Success);
        var session = claude.Session();
        List<Reply> replies = [];

        await foreach (var reply in session.Answer(new Prompt("toca"), Stop))
        {
            replies.Add(reply);
            if (reply is NeedsApproval approval)
            {
                session.Decide(approval.Id, (Crafter.Core.Agents.Decision)Activator.CreateInstance(decision)!);
            }
        }

        Assert.Equal(new NeedsApproval("r-1", "Bash", "touch x", "Create x"), replies[0]);
        Assert.Contains(new Wrote(answer), replies);
    }

    [Fact]
    public async Task TrustsAToolForTheRestOfTheChat()
    {
        using var claude = new FakeClaude(
            "read -r line\n" + Asks("r-1", "first") + "\n" + Asks("r-2", "second") + "\n" + FakeClaude.Writes("$first-$second") + "\n" + FakeClaude.Success);
        var session = claude.Session();
        List<Reply> replies = [];

        await foreach (var reply in session.Answer(new Prompt("toca"), Stop))
        {
            replies.Add(reply);
            if (reply is NeedsApproval approval)
            {
                session.Decide(approval.Id, new Crafter.Core.Agents.AllowTool());
            }
        }

        Assert.Single(replies.OfType<NeedsApproval>());
        Assert.Contains(new Wrote("allowed-allowed"), replies);
    }

    [Fact]
    public async Task StopsWhileWaitingForADecision()
    {
        using var claude = new FakeClaude("read -r line\n" + Asks("r-1", "first") + "\nsleep 30");
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Stop);
        List<Reply> replies = [];

        await foreach (var reply in claude.Session().Answer(new Prompt("toca"), stopping.Token))
        {
            replies.Add(reply);
            if (reply is NeedsApproval)
            {
                await stopping.CancelAsync();
            }
        }

        Assert.IsType<Stopped>(replies[^1]);
    }

    [Fact]
    public void IgnoresADecisionNobodyAskedFor()
    {
        var session = new ClaudeCodeSession(Path.GetTempPath());

        session.Decide("r-404", new Crafter.Core.Agents.Allow());

        Assert.Null(session.SessionId);
    }

    private static string Asks(string request, string verdict) =>
        "echo '{\"type\":\"control_request\",\"request_id\":\"" + request
        + "\",\"request\":{\"subtype\":\"can_use_tool\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"touch x\"},\"description\":\"Create x\"}}'\n"
        + "read -r answer\n"
        + "if [[ \"$answer\" == *'\"behavior\":\"allow\"'*'\"updatedInput\":{\"command\":\"touch x\"}'* ]]; then " + verdict + "=allowed; else " + verdict + "=denied; fi";
}
