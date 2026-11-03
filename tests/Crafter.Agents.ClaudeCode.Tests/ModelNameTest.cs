namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class ModelNameTest
{
    [Theory]
    [InlineData("claude-opus-5-5", "Opus 5.5")]
    [InlineData("claude-sonnet-5", "Sonnet 5")]
    [InlineData("claude-haiku", "Haiku")]
    [InlineData("claude-", "claude-")]
    public void ReadsTheModelId(string id, string name)
    {
        Assert.Equal(name, ModelName.Of(id));
    }
}
