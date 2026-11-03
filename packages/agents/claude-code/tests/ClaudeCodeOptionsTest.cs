namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class ClaudeCodeOptionsTest
{
    private static readonly string[] Streams = ["-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages"];

    [Fact]
    public void ChatsWithoutToolsByDefault()
    {
        Assert.Equal([.. Streams, "--tools", ""], new ClaudeCodeOptions("claude").Arguments(null));
    }

    [Fact]
    public void PlansWithoutEditing()
    {
        Assert.Equal([.. Streams, "--permission-mode", "plan"], new ClaudeCodeOptions("claude", Tools: ClaudeCodeOptions.Plans).Arguments(null));
    }

    [Fact]
    public void AcceptsEditsWhenAllowedToEdit()
    {
        Assert.Equal([.. Streams, "--permission-mode", "acceptEdits"], new ClaudeCodeOptions("claude", Tools: ClaudeCodeOptions.Edits).Arguments(null));
    }

    [Fact]
    public void AsksForTheChosenModelAndEffort()
    {
        Assert.Equal(
            [.. Streams, "--tools", "", "--model", "opus", "--effort", "high"],
            new ClaudeCodeOptions("claude", Model: "opus", Effort: "high").Arguments(null));
    }

    [Fact]
    public void ResumesASession()
    {
        Assert.Equal([.. Streams, "--tools", "", "--resume", "s-1"], new ClaudeCodeOptions("claude").Arguments("s-1"));
    }
}
