using System.Text.Json.Nodes;
using Crafter.Core.Agents;

namespace Crafter.Agents.ClaudeCode.Cli;

// The --mcp-config Claude Code reads: each server over HTTP, with its token as a bearer header.
public static class ClaudeCodeMcp
{
    public static string Config(IReadOnlyList<ToolServer> servers)
    {
        var configured = new JsonObject();
        foreach (var server in servers)
        {
            configured[server.Name] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = server.Endpoint.ToString(),
                ["headers"] = new JsonObject { ["Authorization"] = $"Bearer {server.Token}" },
            };
        }

        return new JsonObject { ["mcpServers"] = configured }.ToJsonString();
    }
}
