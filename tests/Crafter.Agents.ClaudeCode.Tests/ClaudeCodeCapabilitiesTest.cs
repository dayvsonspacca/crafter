using Crafter.Agents.ClaudeCode.Cli;

namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class ClaudeCodeCapabilitiesTest
{
    public const string Help = """
          --effort <level>                      Effort level for the current session
                                                (low, medium, high, xhigh, max)
          --model <model>                       Model for the current session. Provide
                                                an alias for the latest model (e.g.
                                                'fable', 'opus', or 'sonnet') or a
                                                model's full name.
          --permission-mode <mode>              Permission mode to use for the session
                                                (choices: "acceptEdits", "auto",
                                                "bypassPermissions", "manual",
                                                "dontAsk", "plan")
        """;

    public const string Introduction =
        """{"type":"control_response","response":{"subtype":"success","request_id":"crafter","response":{"models":[{"value":"default","displayName":"Default (recommended)"},{"value":"opus","displayName":"Opus 5.5"},{"value":"claude-sonnet-4-6","displayName":"Sonnet 4.6"},{"value":"claude-next"},{"displayName":"No value"}]}}}""";

    [Fact]
    public void ReadsTheChoicesOutOfTheHelp()
    {
        var capabilities = ClaudeCodeCapabilities.Parse(Help);

        Assert.Equal(["fable", "opus", "sonnet"], capabilities.Models.Select(model => model.Value));
        Assert.Equal(["low", "medium", "high", "xhigh", "max"], capabilities.Efforts);
        Assert.Equal(["acceptEdits", "auto", "bypassPermissions", "manual", "dontAsk", "plan"], capabilities.Modes);
    }

    [Fact]
    public void PicksUpChoicesItHasNeverSeen()
    {
        var capabilities = ClaudeCodeCapabilities.Parse(Help.Replace("xhigh, max", "xhigh, max, ultra").Replace("\"plan\"", "\"plan\", \"review\""));

        Assert.Contains("ultra", capabilities.Efforts);
        Assert.Contains("review", capabilities.Modes);
    }

    [Fact]
    public void TakesTheModelsClaudeCodeIntroduces()
    {
        var capabilities = ClaudeCodeCapabilities.Parse(Help).Introduced(Introduction);

        Assert.Equal(
            [new ClaudeCodeModel("opus", "Opus 5.5"), new ClaudeCodeModel("claude-sonnet-4-6", "Sonnet 4.6"), new ClaudeCodeModel("claude-next")],
            capabilities.Models);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{"type":"control_response","response":{"subtype":"error"}}""")]
    [InlineData("""{"type":"control_response","response":{"response":{"models":[]}}}""")]
    public void KeepsTheAliasesWhenTheIntroductionSaysNothing(string? answer)
    {
        Assert.Equal(["fable", "opus", "sonnet"], ClaudeCodeCapabilities.Parse(Help).Introduced(answer).Models.Select(model => model.Value));
    }

    [Fact]
    public void KnowsNothingFromAHelpWithoutThoseOptions()
    {
        Assert.Equal(ClaudeCodeCapabilities.Unknown.Models, ClaudeCodeCapabilities.Parse("Usage: claude [options]").Models);
        Assert.Empty(ClaudeCodeCapabilities.Parse(string.Empty).Efforts);
    }
}
