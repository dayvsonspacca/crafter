namespace Crafter.Core.Agents;

// A tool server an agent connects to for one session: where it listens, the token that names the
// session, the tools the agent may use without asking, and how long a call may take, for tools that
// wait on the user.
public sealed record ToolServer(string Name, Uri Endpoint, string Token, IReadOnlyList<string> FreeTools, TimeSpan? Patience = null);
