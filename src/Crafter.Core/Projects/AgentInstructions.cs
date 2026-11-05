namespace Crafter.Core.Projects;

// An agent's own instructions file a project brings. Crafter turns those files off, so they can only
// reach the agent as the start of the project's summary.
public sealed record AgentInstructions(string File, string Text)
{
    // The file meant for any agent, preferred over each agent's own.
    public const string Shared = "AGENTS.md";
}
