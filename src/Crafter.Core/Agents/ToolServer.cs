namespace Crafter.Core.Agents;

// A tool server an agent connects to for one project: where it listens, the token that names the
// project, and the tools that only read, which the agent may use without asking.
public sealed record ToolServer(string Name, Uri Endpoint, string Token, IReadOnlyList<string> FreeTools);
