using System.Text.Json.Nodes;

namespace Crafter.Mcp.Tests;

public sealed class McpCallTest
{
    [Theory]
    [InlineData("""{ "number": 3 }""", 3)]
    [InlineData("""{ "number": "3" }""", 3)]
    [InlineData("""{ "number": " #3 " }""", 3)]
    [InlineData("""{ "number": "three" }""", null)]
    [InlineData("""{ "number": 3.5 }""", null)]
    [InlineData("""{ }""", null)]
    public void ReadsANumberSentAsOneOrAsText(string arguments, int? number)
    {
        var call = new McpCall(JsonNode.Parse(arguments)!.AsObject(), "/home/hero/crafter");

        Assert.Equal(number, call.Number("number"));
    }
}
