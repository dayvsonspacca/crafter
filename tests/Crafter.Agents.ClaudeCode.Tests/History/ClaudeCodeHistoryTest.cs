using System.Text.Json;
using Crafter.Agents.ClaudeCode.History;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Projects;

namespace Crafter.Agents.ClaudeCode.Tests.History;

public sealed class ClaudeCodeHistoryTest : IDisposable
{
    private static readonly Project Crafter = new("/home/hero/Projetos/me/desktop/crafter");

    private readonly string home = Directory.CreateTempSubdirectory("crafter-claude-home-").FullName;

    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    private string Sessions => Path.Combine(home, "projects", "-home-hero-Projetos-me-desktop-crafter");

    [Fact]
    public async Task HasNoHistoryForAProjectItNeverSaw()
    {
        var history = await new ClaudeCodeHistory(home).List(Crafter, 5, Stop);

        Assert.Empty(history.Latest);
        Assert.Equal(0, history.Total);
    }

    [Fact]
    public async Task ListsSessionsByTheirFirstPromptNewestFirst()
    {
        Write("old", DateTime.UtcNow.AddDays(-2), User("Explain the crew"), Assistant("It hires agents."));
        Write("new", DateTime.UtcNow, Queue(), User("Fix reconnect after TimedOut\nand cover it"));

        var chats = (await new ClaudeCodeHistory(home).List(Crafter, 5, Stop)).Latest;

        Assert.Equal(["new", "old"], chats.Select(chat => chat.SessionId));
        Assert.Equal(["Fix reconnect after TimedOut", "Explain the crew"], chats.Select(chat => chat.Title.Text));
    }

    [Fact]
    public async Task RemembersTheLastPromptOfALongerChat()
    {
        Write("long", DateTime.UtcNow, User("Fix it"), Assistant("Done."), User("Now test it"), Assistant("Tested."));
        Write("short", DateTime.UtcNow.AddMinutes(-1), User("Explain the crew"));

        var chats = (await new ClaudeCodeHistory(home).List(Crafter, 5, Stop)).Latest;

        Assert.Equal(new Prompt("Now test it"), chats[0].LastPrompt);
        Assert.Null(chats[1].LastPrompt);
    }

    [Fact]
    public async Task SkipsSessionsWithoutAPrompt()
    {
        Write("empty", DateTime.UtcNow, Queue(), User("<command-name>/clear</command-name>"), Meta("Caveat"), "not json", "[]", "{\"type\":\"user\", broken");

        Assert.Empty((await new ClaudeCodeHistory(home).List(Crafter, 5, Stop)).Latest);
    }

    [Fact]
    public async Task LeavesOutCompactionSummariesAndTranscriptOnlyEntries()
    {
        Write(
            "s-1",
            DateTime.UtcNow,
            Flagged("isCompactSummary", "This session is being continued from a previous conversation"),
            User("Fix it"),
            Assistant("Done."),
            Flagged("isVisibleInTranscriptOnly", "Shown only in the transcript"));

        var chat = Assert.Single((await new ClaudeCodeHistory(home).List(Crafter, 5, Stop)).Latest);
        var turns = await new ClaudeCodeHistory(home).Replay(Crafter, "s-1", Stop);

        Assert.Equal("Fix it", chat.Title.Text);
        Assert.Null(chat.LastPrompt);
        Assert.Equal([new Asked(new Prompt("Fix it")), new Answered("Done.", new Finished())], turns);
    }

    [Fact]
    public async Task FindsTheLastPromptAtTheEndOfAHugeSession()
    {
        var noise = JsonSerializer.Serialize(new { type = "user", message = new { role = "user", content = new[] { new { type = "tool_result", content = new string('x', 200_000) } } } });
        Write("huge", DateTime.UtcNow, [User("Start here"), .. Enumerable.Repeat(noise, 4), User("And finish there"), Assistant("Finished.")]);

        var chat = Assert.Single((await new ClaudeCodeHistory(home).List(Crafter, 5, Stop)).Latest);

        Assert.Equal("Start here", chat.Title.Text);
        Assert.Equal(new Prompt("And finish there"), chat.LastPrompt);
    }

    [Fact]
    public async Task KeepsTheLatestAndCountsTheRest()
    {
        for (var at = 0; at < 8; at++)
        {
            Write($"s-{at}", DateTime.UtcNow.AddMinutes(at), User($"Prompt {at}"));
        }

        Write("empty", DateTime.UtcNow.AddMinutes(-5), Queue());

        var history = await new ClaudeCodeHistory(home).List(Crafter, 5, Stop);

        Assert.Equal(["s-7", "s-6", "s-5", "s-4", "s-3"], history.Latest.Select(chat => chat.SessionId));
        Assert.Equal(8, history.Total);
    }

    [Fact]
    public async Task ReplaysPromptsAndTheTextOfTheAnswers()
    {
        Write(
            "s-1",
            DateTime.UtcNow,
            Assistant("before any prompt"),
            User("Fix it"),
            Assistant("Looking."),
            ToolResult(),
            Assistant("Fixed."),
            Sidechain("Agent task"),
            User(new[] { new { type = "text", text = (string?)"And this" }, new { type = "image", text = (string?)null } }),
            ToolUseOnly(),
            User("Thanks"),
            Assistant("Anytime."));

        var turns = await new ClaudeCodeHistory(home).Replay(Crafter, "s-1", Stop);

        Assert.Equal(
            [
                new Asked(new Prompt("Fix it")),
                new Answered("Looking.\n\nFixed.", new Finished()),
                new Asked(new Prompt("And this")),
                new Asked(new Prompt("Thanks")),
                new Answered("Anytime.", new Finished()),
            ],
            turns);
    }

    [Fact]
    public async Task ReplaysWhatTheUserTypedAndLeavesOutWhatClaudeCodeInjected()
    {
        Write(
            "s-1",
            DateTime.UtcNow,
            User("<div>Why is this tag here?</div>"),
            Assistant("It wraps the page."),
            User("<command-name>/compact</command-name>\n<command-message>compact</command-message>\n<command-args>keep the tests</command-args>"),
            User("<local-command-stdout>Compacted</local-command-stdout>"),
            User("<command-name>/clear</command-name>\n<command-args></command-args>"),
            User("<bash-input>ls</bash-input>"),
            User("<bash-stdout>README.md</bash-stdout><bash-stderr></bash-stderr>"),
            User("<task-notification><summary>done</summary></task-notification>"),
            User("Caveat: The messages below were generated by the user while running local commands."),
            User("[Request interrupted by user for tool use]"),
            User("<system-reminder>Be brief.</system-reminder>\nAnd now?"),
            User("<system-reminder>Only a reminder.</system-reminder>"),
            Assistant("Now this."));

        var turns = await new ClaudeCodeHistory(home).Replay(Crafter, "s-1", Stop);

        Assert.Equal(
            [
                new Asked(new Prompt("<div>Why is this tag here?</div>")),
                new Answered("It wraps the page.", new Finished()),
                new Asked(new Prompt("/compact keep the tests")),
                new Asked(new Prompt("/clear")),
                new Asked(new Prompt("! ls")),
                new Asked(new Prompt("And now?")),
                new Answered("Now this.", new Finished()),
            ],
            turns);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("../escape")]
    public async Task ReplaysNothingForASessionItCannotFind(string sessionId)
    {
        Assert.Empty(await new ClaudeCodeHistory(home).Replay(Crafter, sessionId, Stop));
    }

    [Fact]
    public async Task LivesInTheClaudeFolderOfTheUser()
    {
        Assert.EndsWith(".claude", ClaudeCodeHistory.Home);
        Assert.NotNull(await new ClaudeCode(home: home).History(Crafter, 5, Stop));
        Assert.Empty(await new ClaudeCode(home: home).Replay(Crafter, "missing", Stop));
    }

    public void Dispose() => Directory.Delete(home, recursive: true);

    private static string User(object content) =>
        JsonSerializer.Serialize(new { type = "user", isSidechain = false, message = new { role = "user", content } });

    private static string Flagged(string flag, string text) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "user", [flag] = true, ["message"] = new { role = "user", content = text } });

    private static string Meta(string text) =>
        JsonSerializer.Serialize(new { type = "user", isMeta = true, message = new { role = "user", content = text } });

    private static string Sidechain(string text) =>
        JsonSerializer.Serialize(new { type = "user", isSidechain = true, message = new { role = "user", content = text } });

    private static string ToolResult() =>
        JsonSerializer.Serialize(new { type = "user", message = new { role = "user", content = new[] { new { type = "tool_result", content = "ok" } } } });

    private static string Assistant(string text) =>
        JsonSerializer.Serialize(new { type = "assistant", message = new { role = "assistant", content = new[] { new { type = "text", text } } } });

    private static string ToolUseOnly() =>
        JsonSerializer.Serialize(new { type = "assistant", message = new { role = "assistant", content = new[] { new { type = "tool_use", text = (string?)null } } } });

    private static string Queue() => JsonSerializer.Serialize(new { type = "queue-operation", operation = "enqueue" });

    private void Write(string sessionId, DateTime touched, params string[] lines)
    {
        Directory.CreateDirectory(Sessions);
        var file = Path.Combine(Sessions, sessionId + ".jsonl");
        File.WriteAllLines(file, lines);
        File.SetLastWriteTimeUtc(file, touched);
    }
}
