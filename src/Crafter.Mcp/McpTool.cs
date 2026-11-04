using System.Text.Json.Nodes;

namespace Crafter.Mcp;

// A tool the server offers. Read-only tools are marked so, and agents may use them unasked.
public sealed record McpTool(string Name, string Description, JsonObject InputSchema, bool ReadOnly, Func<McpCall, Task<McpResult>> Run);

// One call: the arguments the agent sent, who it is, and the token it carried.
public sealed record McpCall(JsonObject Arguments, string Caller, string Token = "")
{
    public string? Text(string name) => Arguments[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

public sealed record McpResult(string Text, bool IsError = false)
{
    public static McpResult Failed(string reason) => new(reason, IsError: true);
}
