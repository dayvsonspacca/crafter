using Crafter.Agents.ClaudeCode.Cli;

namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class ClaudeCodeOptionsTest
{
    private static readonly string[] Streams =
    [
        "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
        "--permission-prompt-tool", "stdio",
    ];

    [Fact]
    public void EditsFilesByDefault()
    {
        Assert.Equal([.. Streams, "--permission-mode", "acceptEdits"], new ClaudeCodeOptions("claude").Arguments(null));
    }

    [Fact]
    public void ChatsWithoutTools()
    {
        Assert.Equal([.. Streams, "--tools", ""], new ClaudeCodeOptions("claude", Tools: ClaudeCodeOptions.Chats).Arguments(null));
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("auto")]
    [InlineData("review")]
    public void PassesAnyOtherModeAlong(string mode)
    {
        Assert.Equal([.. Streams, "--permission-mode", mode], new ClaudeCodeOptions("claude", Tools: mode).Arguments(null));
    }

    [Fact]
    public void ReadsTheEditChoiceSavedBeforeModesCameFromTheHelp()
    {
        Assert.Equal([.. Streams, "--permission-mode", "acceptEdits"], new ClaudeCodeOptions("claude", Tools: "edit").Arguments(null));
    }

    [Fact]
    public void AsksForTheChosenModelAndEffort()
    {
        Assert.Equal(
            [.. Streams, "--permission-mode", "acceptEdits", "--model", "opus", "--effort", "high"],
            new ClaudeCodeOptions("claude", Model: "opus", Effort: "high").Arguments(null));
    }

    [Fact]
    public void ResumesASession()
    {
        Assert.Equal([.. Streams, "--permission-mode", "acceptEdits", "--resume", "s-1"], new ClaudeCodeOptions("claude").Arguments("s-1"));
    }
}
