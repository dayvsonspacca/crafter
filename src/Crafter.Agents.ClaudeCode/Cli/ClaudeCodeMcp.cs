using System.Text.Json.Nodes;
using Crafter.Core.Agents;

namespace Crafter.Agents.ClaudeCode.Cli;

// The --mcp-config Claude Code reads: each server over HTTP, with its token as a bearer header, and
// the time a call may take when the server asks for more than Claude Code's default.
public static class ClaudeCodeMcp
{
    public static string Config(IReadOnlyList<ToolServer> servers)
    {
        var configured = new JsonObject();
        foreach (var server in servers)
        {
            var entry = new JsonObject
            {
                ["type"] = "http",
                ["url"] = server.Endpoint.ToString(),
                ["headers"] = new JsonObject { ["Authorization"] = $"Bearer {server.Token}" },
            };
            if (server.Patience is { } patience)
            {
                entry["timeout"] = (long)patience.TotalMilliseconds;
            }

            configured[server.Name] = entry;
        }

        return new JsonObject { ["mcpServers"] = configured }.ToJsonString();
    }
}
