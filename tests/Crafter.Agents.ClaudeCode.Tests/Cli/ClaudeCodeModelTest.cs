using Crafter.Agents.ClaudeCode.Cli;

namespace Crafter.Agents.ClaudeCode.Tests.Cli;

public sealed class ClaudeCodeModelTest
{
    [Theory]
    [InlineData("claude-opus-5-5", "Opus 5.5")]
    [InlineData("claude-sonnet-5", "Sonnet 5")]
    [InlineData("claude-haiku", "Haiku")]
    [InlineData("claude-", "claude-")]
    public void ReadsTheModelId(string id, string name)
    {
        Assert.Equal(name, ClaudeCodeModel.NameOf(id));
    }
}
