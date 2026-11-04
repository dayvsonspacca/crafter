using System.Text.Json;
using Crafter.Agents.ClaudeCode.Protocol;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;

namespace Crafter.Agents.ClaudeCode.Tests.Protocol;

public sealed class ClaudeCodeProtocolTest
{
    private const string Input = """{"command":"npm test"}""";

    private const string Suggestions = """[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"npm test:*"}],"behavior":"allow","destination":"session"}]""";

    [Fact]
    public void AsksAsTheUser()
    {
        using var asked = JsonDocument.Parse(ClaudeCodeProtocol.Asking(new Prompt("oi")));

        Assert.Equal("user", asked.RootElement.GetProperty("type").GetString());
        Assert.Equal("oi", asked.RootElement.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public void ShowsThePicturesBeforeTheText()
    {
        using var asked = JsonDocument.Parse(ClaudeCodeProtocol.Asking(new Prompt("What is in [Image #1]?", [new Picture("image/png", [1, 2, 3])])));
        var content = asked.RootElement.GetProperty("message").GetProperty("content");

        Assert.Equal("image", content[0].GetProperty("type").GetString());
        Assert.Equal("base64", content[0].GetProperty("source").GetProperty("type").GetString());
        Assert.Equal("image/png", content[0].GetProperty("source").GetProperty("media_type").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), content[0].GetProperty("source").GetProperty("data").GetString());
        Assert.Equal("text", content[1].GetProperty("type").GetString());
        Assert.Equal("What is in [Image #1]?", content[1].GetProperty("text").GetString());
    }

    [Fact]
    public void AllowsWithTheInputItWasAskedFor()
    {
        var response = Response(new Allow(), asked: false);

        Assert.Equal("allow", response.GetProperty("behavior").GetString());
        Assert.Equal("npm test", response.GetProperty("updatedInput").GetProperty("command").GetString());
        Assert.False(response.TryGetProperty("updatedPermissions", out _));
    }

    [Fact]
    public void AlwaysAllowingHandsBackTheSuggestions()
    {
        var response = Response(new AlwaysAllow(), asked: false);

        Assert.Equal("Bash", response.GetProperty("updatedPermissions")[0].GetProperty("rules")[0].GetProperty("toolName").GetString());
    }

    [Theory]
    [InlineData(false, null, "The user denied this in Crafter. Don't try it again; take another way or ask them how to proceed.")]
    [InlineData(false, "use pnpm", "The user denied this in Crafter and said: use pnpm")]
    [InlineData(true, null, "The user chose not to answer. Carry on with your best judgment or ask in plain text.")]
    [InlineData(true, " later ", "The user chose not to answer and said: later")]
    public void DenyingSaysWhy(bool asked, string? reason, string message)
    {
        var response = Response(new Deny(reason), asked);

        Assert.Equal("deny", response.GetProperty("behavior").GetString());
        Assert.Equal(message, response.GetProperty("message").GetString());
    }

    [Fact]
    public void AnswersReplaceTheOnesInTheInput()
    {
        var response = Response(
            new Answers(new Dictionary<string, string> { ["Which runner?"] = "xunit" }),
            asked: true,
            input: """{"questions":[],"answers":{"stale":"yes"}}""");

        var input = response.GetProperty("updatedInput");
        Assert.Equal(JsonValueKind.Array, input.GetProperty("questions").ValueKind);
        Assert.Equal("xunit", input.GetProperty("answers").GetProperty("Which runner?").GetString());
        Assert.False(input.GetProperty("answers").TryGetProperty("stale", out _));
    }

    [Fact]
    public void RespondsToTheRequestItWasAsked()
    {
        using var answer = JsonDocument.Parse(ClaudeCodeProtocol.Answering("r-7", new Allow(), Input, null, asked: false));

        Assert.Equal("control_response", answer.RootElement.GetProperty("type").GetString());
        Assert.Equal("r-7", answer.RootElement.GetProperty("response").GetProperty("request_id").GetString());
    }

    private static JsonElement Response(Decision decision, bool asked, string input = Input)
    {
        using var answer = JsonDocument.Parse(ClaudeCodeProtocol.Answering("r-1", decision, input, Suggestions, asked));

        return answer.RootElement.GetProperty("response").GetProperty("response").Clone();
    }
}
