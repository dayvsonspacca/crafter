using Crafter.Core.Conversations;

namespace Crafter.ClaudeCode.Tests;

public sealed class ClaudeCodeSessionTest
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnswersWithWhatClaudeCodeStreams()
    {
        using var claude = new FakeClaude($"cat > /dev/null\n{FakeClaude.Init}\n{FakeClaude.Writes("Olá")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal([new Began("claude-opus-5-5"), new Wrote("Olá"), new Finished()], replies);
    }

    [Fact]
    public async Task SendsThePromptThroughStandardInput()
    {
        using var claude = new FakeClaude($"prompt=$(cat)\n{FakeClaude.Writes("$prompt")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("--help"), Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("--help"), replies);
    }

    [Fact]
    public async Task ResumesTheSessionOnTheNextQuestion()
    {
        using var claude = new FakeClaude(
            $"""
            cat > /dev/null
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
    public async Task LeavesTheParentClaudeCodeSessionBehind()
    {
        Environment.SetEnvironmentVariable("CLAUDECODE", "1");
        using var claude = new FakeClaude($"cat > /dev/null\n{FakeClaude.Writes("${CLAUDECODE:-clean}")}\n{FakeClaude.Success}");

        var replies = await claude.Session().Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Contains(new Wrote("clean"), replies);
    }

    [Fact]
    public async Task StopsWhenAsked()
    {
        using var claude = new FakeClaude($"cat > /dev/null\n{FakeClaude.Init}\nsleep 30");
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

        Assert.Equal([new Began("claude-opus-5-5"), new Stopped()], replies);
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
        using var claude = new FakeClaude("cat > /dev/null\necho 'Not logged in' >&2\nexit 1");

        var replies = await claude.Session().Answer(new Prompt("oi"), Stop).ToListAsync(Stop);

        Assert.Equal([new Failed("Not logged in")], replies);
    }

    [Fact]
    public async Task FailsWithTheExitCodeWhenClaudeCodeSaysNothing()
    {
        using var claude = new FakeClaude("cat > /dev/null\nexit 3");

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
}
