using System.Text.Json;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Agents.ClaudeCode.Tests;

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
        Write("empty", DateTime.UtcNow, Queue(), User("<command-name>/clear</command-name>"), Meta("Caveat"), "not json", "[]");

        Assert.Empty((await new ClaudeCodeHistory(home).List(Crafter, 5, Stop)).Latest);
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
