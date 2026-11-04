using Crafter.Agents.ClaudeCode.Cli;
using Crafter.Core.Agents;
using Crafter.Core.Agents.Background;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class ClaudeCodeSessionTest
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnswersWithWhatClaudeCodeStreams()
    {
        using var claude = new FakeClaude($"read -r line\n{FakeClaude.Init}\n{FakeClaude.Writes("Olá")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal([new Began("Opus 5.5"), new Wrote("Olá"), new Finished()], replies);
    }

    [Fact]
    public async Task ConnectsTheProjectsToolServersAndLetsTheirReadingToolsRunUnasked()
    {
        using var claude = new FakeClaude($"printf '%s\\n' \"$@\" > \"$(dirname \"$0\")/arguments\"\nread -r line\n{FakeClaude.Init}\n{FakeClaude.Success}");
        ToolServer[] servers = [new("crafter", new Uri("http://127.0.0.1:4100/mcp"), "t0ken", ["list_projects", "read_skill"])];
        var session = new ClaudeCodeSession(claude.Folder, _ => new ClaudeCodeOptions(claude.Executable), servers: () => servers);

        await session.Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        var arguments = File.ReadAllLines(Path.Combine(claude.Folder, "arguments"));
        var config = arguments[Array.IndexOf(arguments, "--mcp-config") + 1];
        Assert.Equal(
            """{"mcpServers":{"crafter":{"type":"http","url":"http://127.0.0.1:4100/mcp","headers":{"Authorization":"Bearer t0ken"}}}}""",
            config);
        Assert.Equal(["mcp__crafter__list_projects", "mcp__crafter__read_skill"], arguments.SkipWhile(each => each != "--allowedTools").Skip(1).Take(2));
    }

    [Fact]
    public async Task HandsTheProjectsSkillsOverAsAPlugin()
    {
        using var claude = new FakeClaude($"printf '%s\\n' \"$@\" > \"$(dirname \"$0\")/arguments\"\nread -r line\n{FakeClaude.Init}\n{FakeClaude.Success}");
        var session = new ClaudeCodeSession(claude.Folder, _ => new ClaudeCodeOptions(claude.Executable), skills: () => "/plugins/crafter");

        await session.Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        var arguments = File.ReadAllLines(Path.Combine(claude.Folder, "arguments"));
        Assert.Equal("/plugins/crafter", arguments[Array.IndexOf(arguments, "--plugin-dir") + 1]);
    }

    [Fact]
    public async Task ConnectsNoServerToAProjectWithoutOne()
    {
        using var claude = new FakeClaude($"printf '%s\\n' \"$@\" > \"$(dirname \"$0\")/arguments\"\nread -r line\n{FakeClaude.Init}\n{FakeClaude.Success}");

        await claude.Session().Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.DoesNotContain("--mcp-config", File.ReadAllLines(Path.Combine(claude.Folder, "arguments")));
        Assert.DoesNotContain("--plugin-dir", File.ReadAllLines(Path.Combine(claude.Folder, "arguments")));
    }

    [Fact]
    public async Task SendsThePromptThroughStandardInput()
    {
        using var claude = new FakeClaude(
            $"read -r line\nif [[ \"$line\" == *'\"type\":\"user\"'*'\"content\":\"--help\"'* ]]; then text=heard; else text=missed; fi\n{FakeClaude.Writes("$text")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("--help"), Configuration.Empty, Stop).ToListAsync(Stop);

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

        var first = await session.Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);
        var second = await session.Answer(new Prompt("e agora?"), Configuration.Empty, Stop).ToListAsync(Stop);

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

        var replies = await session.Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("resumed"), replies);
    }

    [Fact]
    public async Task KnowsItsSessionOnceClaudeCodeTellsIt()
    {
        using var claude = new FakeClaude($"read -r line\n{FakeClaude.Init}\nsleep 30");
        using var asked = CancellationTokenSource.CreateLinkedTokenSource(Stop);
        var session = claude.Session();

        Assert.Null(session.SessionId);
        await foreach (var reply in session.Answer(new Prompt("oi"), Configuration.Empty, asked.Token))
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

        var replies = await claude.Session().Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("clean"), replies);
    }

    [Fact]
    public async Task StopsWhenAsked()
    {
        using var claude = new FakeClaude($"read -r line\n{FakeClaude.Init}\nsleep 30");
        using var asked = CancellationTokenSource.CreateLinkedTokenSource(Stop);
        var replies = new List<Reply>();

        await foreach (var reply in claude.Session().Answer(new Prompt("oi"), Configuration.Empty, asked.Token))
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

        var replies = await session.Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.IsType<Failed>(Assert.Single(replies));
    }

    [Fact]
    public async Task FailsWithWhatClaudeCodeComplained()
    {
        using var claude = new FakeClaude("read -r line\necho 'Not logged in' >&2\nexit 1");

        var replies = await claude.Session().Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal([new Failed("Not logged in")], replies);
    }

    [Fact]
    public async Task FailsWithTheExitCodeWhenClaudeCodeSaysNothing()
    {
        using var claude = new FakeClaude("read -r line\nexit 3");

        var replies = await claude.Session().Answer(new Prompt("oi"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal([new Failed("Claude Code exited with code 3 without an answer.")], replies);
    }

    [Fact]
    public async Task FailsWhenClaudeCodeLeavesBeforeListening()
    {
        using var claude = new FakeClaude("exit 4");

        var replies = await claude.Session().Answer(new Prompt(new string('x', 1 << 20)), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.IsType<Failed>(Assert.Single(replies));
    }

    [Theory]
    [InlineData(true, "allowed")]
    [InlineData(false, "denied")]
    public async Task AsksBeforeRunningWhatNeedsApproval(bool allow, string answer)
    {
        using var claude = new FakeClaude("read -r line\n" + Asks("r-1", "first") + "\n" + FakeClaude.Writes("$first") + "\n" + FakeClaude.Success);
        var session = claude.Session();
        List<Reply> replies = [];

        await foreach (var reply in session.Answer(new Prompt("toca"), Configuration.Empty, Stop))
        {
            replies.Add(reply);
            if (reply is NeedsApproval approval)
            {
                session.Decide(approval.Id, allow ? new Allow() : new Deny());
            }
        }

        Assert.Equal(new NeedsApproval("r-1", "Bash", "touch x", "Create x"), replies[0]);
        Assert.Contains(new Wrote(answer), replies);
    }

    [Fact]
    public async Task RemembersWhatClaudeCodeSuggestedWhenAlwaysAllowed()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + "if [[ \" $* \" == *\" --allowedTools Bash(touch:*) \"* ]]; then first=remembered; else\n"
            + "echo '{\"type\":\"control_request\",\"request_id\":\"r-1\",\"request\":{\"subtype\":\"can_use_tool\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"touch x\"},\"permission_suggestions\":" + Suggested + "}}'\n"
            + "read -r answer\n"
            + "if [[ \"$answer\" == *'\"updatedPermissions\":[{\"type\":\"addRules\"'* ]]; then first=always; else first=once; fi\n"
            + "fi\n"
            + FakeClaude.Writes("$first") + "\n" + FakeClaude.Success);
        var session = claude.Session();

        var first = await Decided(session, new AlwaysAllow());
        var second = await Decided(session, new Allow());

        Assert.Equal("Bash(touch:*) in this chat", first.OfType<NeedsApproval>().Single().Remembers);
        Assert.Contains(new Wrote("always"), first);
        Assert.Contains(new Wrote("remembered"), second);
    }

    [Fact]
    public async Task AllowingOnceRemembersNothing()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + "echo '{\"type\":\"control_request\",\"request_id\":\"r-1\",\"request\":{\"subtype\":\"can_use_tool\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"touch x\"},\"permission_suggestions\":" + Suggested + "}}'\n"
            + "read -r answer\n"
            + "if [[ \"$answer\" == *updatedPermissions* || \" $* \" == *--allowedTools* ]]; then first=remembered; else first=once; fi\n"
            + FakeClaude.Writes("$first") + "\n" + FakeClaude.Success);
        var session = claude.Session();

        await Decided(session, new Allow());
        var second = await Decided(session, new Allow());

        Assert.Contains(new Wrote("once"), second);
    }

    [Fact]
    public async Task TellsClaudeCodeWhyItWasDenied()
    {
        using var claude = new FakeClaude(
            "read -r line\n" + Asks("r-1", "first") + "\n"
            + "if [[ \"$answer\" == *'\"message\":\"The user denied this in Crafter and said: use mkdir\"'* ]]; then why=told; else why=silent; fi\n"
            + FakeClaude.Writes("$why") + "\n" + FakeClaude.Success);

        var replies = await Decided(claude.Session(), new Deny("  use mkdir "));

        Assert.Contains(new Wrote("told"), replies);
    }

    [Fact]
    public async Task HandsTheAnswersBackWithTheQuestions()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + "echo '{\"type\":\"control_request\",\"request_id\":\"r-1\",\"request\":{\"subtype\":\"can_use_tool\",\"tool_name\":\"AskUserQuestion\",\"input\":{\"questions\":[{\"question\":\"Which?\",\"header\":\"Pick\",\"multiSelect\":true,\"options\":[{\"label\":\"A\"},{\"label\":\"B\"}]}]}}}'\n"
            + "read -r answer\n"
            + "if [[ \"$answer\" == *'\"behavior\":\"allow\"'*'\"questions\":[{\"question\":\"Which?\"'*'\"answers\":{\"Which?\":\"A, B\"}'* ]]; then heard=answered; else heard=lost; fi\n"
            + FakeClaude.Writes("$heard") + "\n" + FakeClaude.Success);
        var session = claude.Session();
        List<Reply> replies = [];

        await foreach (var reply in session.Answer(new Prompt("pergunta"), Configuration.Empty, Stop))
        {
            replies.Add(reply);
            if (reply is NeedsAnswers asked)
            {
                session.Decide(asked.Id, new Answers(new Dictionary<string, string> { ["Which?"] = "A, B" }));
            }
        }

        Assert.IsType<NeedsAnswers>(replies[0]);
        Assert.Contains(new Wrote("answered"), replies);
    }

    [Fact]
    public async Task LetsGoOfARequestClaudeCodeWithdraws()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + "echo '{\"type\":\"control_request\",\"request_id\":\"r-1\",\"request\":{\"subtype\":\"can_use_tool\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"touch x\"}}}'\n"
            + "echo '{\"type\":\"control_cancel_request\",\"request_id\":\"r-1\"}'\n"
            + FakeClaude.Writes("moved on") + "\n" + FakeClaude.Success);

        var replies = await claude.Session().Answer(new Prompt("toca"), Configuration.Empty, Stop).ToListAsync(Stop);

        Assert.Equal("r-1", Assert.IsType<NeedsApproval>(replies[0]).Id);
        Assert.Equal([new Withdrawn("r-1"), new Wrote("moved on"), new Finished()], replies[1..]);
    }

    [Fact]
    public async Task KeepsWhatClaudeCodeSaidWhileWaiting()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + "echo '{\"type\":\"control_request\",\"request_id\":\"r-1\",\"request\":{\"subtype\":\"can_use_tool\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"touch x\"}}}'\n"
            + FakeClaude.Writes("meanwhile") + "\n"
            + "read -r answer\n"
            + FakeClaude.Writes(" after") + "\n" + FakeClaude.Success);

        var replies = await Decided(claude.Session(), new Allow());

        Assert.Equal([new Wrote("meanwhile"), new Wrote(" after"), new Finished()], replies[1..]);
    }

    [Fact]
    public async Task StopsWhileWaitingForADecision()
    {
        using var claude = new FakeClaude("read -r line\n" + Asks("r-1", "first") + "\nsleep 30");
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Stop);
        List<Reply> replies = [];

        await foreach (var reply in claude.Session().Answer(new Prompt("toca"), Configuration.Empty, stopping.Token))
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

        session.Decide("r-404", new Allow());

        Assert.Null(session.SessionId);
    }

    [Fact]
    public async Task EndsTheTurnWhileAJobRunsAndWakesWhenItSettles()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + FakeClaude.Init + "\n"
            + Job("b-1") + "\n"
            + FakeClaude.Writes("started") + "\n" + FakeClaude.Success + "\n"
            + "sleep 0.3\n"
            + "echo '{\"type\":\"system\",\"subtype\":\"task_notification\",\"task_id\":\"b-1\",\"status\":\"completed\"}'\n"
            + FakeClaude.Init + "\n" + FakeClaude.Writes("it finished") + "\n" + FakeClaude.Success + "\n"
            + "read -r line");
        var session = claude.Session();
        var woken = new TaskCompletionSource<IAsyncEnumerable<Reply>>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<IReadOnlyList<Job>> changes = [];
        session.Woke += (_, replies) => woken.TrySetResult(replies);
        session.JobsChanged += (_, jobs) => changes.Add(jobs);

        var replies = await session.Answer(new Prompt("start the server"), Configuration.Empty, Stop).ToListAsync(Stop);
        var running = session.Jobs;
        var later = await (await woken.Task.WaitAsync(Stop)).ToListAsync(Stop);

        Assert.Equal([new Began("Opus 5.5"), new Wrote("started"), new Finished()], replies);
        Assert.Equal([new Job("b-1", JobKind.Shell, "Dev server")], running);
        Assert.Equal([new Began("Opus 5.5"), new Wrote("it finished"), new Finished()], later);
        Assert.Empty(changes[^1]);
    }

    [Fact]
    public async Task AsksAgainInTheSameRunWhileAJobRuns()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + Starts() + "\n"
            + Job("b-1") + "\n"
            + FakeClaude.Writes("first") + "\n" + FakeClaude.Success + "\n"
            + "read -r line\n"
            + Starts() + "\n"
            + FakeClaude.Writes("same run") + "\n" + FakeClaude.Success + "\n"
            + "read -r line");
        var session = claude.Session();

        await session.Answer(new Prompt("start the server"), Configuration.Empty, Stop).ToListAsync(Stop);
        var second = await session.Answer(new Prompt("and now?"), Configuration.Empty, Stop).ToListAsync(Stop);
        session.Dismiss();

        Assert.Equal([new Wrote("same run"), new Finished()], second);
    }

    [Fact]
    public async Task AsksClaudeCodeToStopAJob()
    {
        using var claude = new FakeClaude(
            "read -r line\n"
            + Job("b-1") + "\n"
            + FakeClaude.Success + "\n"
            + "read -r line\n"
            + "if [[ \"$line\" == *'\"subtype\":\"stop_task\"'*'\"task_id\":\"b-1\"'* ]]; then echo '{\"type\":\"system\",\"subtype\":\"background_tasks_changed\",\"tasks\":[]}'; fi\n"
            + "read -r line");
        var session = claude.Session();
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.JobsChanged += (_, jobs) =>
        {
            if (jobs.Count == 0)
            {
                settled.TrySetResult();
            }
        };

        await session.Answer(new Prompt("start the server"), Configuration.Empty, Stop).ToListAsync(Stop);
        session.Stop("b-1");
        await settled.Task.WaitAsync(Stop);
        session.Dismiss();

        Assert.Empty(session.Jobs);
    }

    private static string Job(string id) =>
        "echo '{\"type\":\"system\",\"subtype\":\"task_started\",\"task_id\":\"" + id + "\",\"description\":\"Dev server\",\"task_type\":\"local_bash\"}'";

    private static string Starts() =>
        "id=$(sed -E 's/.*\"uuid\":\"([^\"]+)\".*/\\1/' <<< \"$line\")\n"
        + "echo \"{\\\"type\\\":\\\"command_lifecycle\\\",\\\"state\\\":\\\"started\\\",\\\"command_uuid\\\":\\\"$id\\\"}\"";

    private const string Suggested = "[{\"type\":\"addRules\",\"rules\":[{\"toolName\":\"Bash\",\"ruleContent\":\"touch:*\"}],\"behavior\":\"allow\",\"destination\":\"session\"}]";

    private static async Task<List<Reply>> Decided(ClaudeCodeSession session, Decision decision)
    {
        List<Reply> replies = [];
        await foreach (var reply in session.Answer(new Prompt("toca"), Configuration.Empty, Stop))
        {
            replies.Add(reply);
            if (reply is NeedsApproval approval)
            {
                session.Decide(approval.Id, decision);
            }
        }

        return replies;
    }

    private static string Asks(string request, string verdict) =>
        "echo '{\"type\":\"control_request\",\"request_id\":\"" + request
        + "\",\"request\":{\"subtype\":\"can_use_tool\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"touch x\"},\"description\":\"Create x\"}}'\n"
        + "read -r answer\n"
        + "if [[ \"$answer\" == *'\"behavior\":\"allow\"'*'\"updatedInput\":{\"command\":\"touch x\"}'* ]]; then " + verdict + "=allowed; else " + verdict + "=denied; fi";
}
