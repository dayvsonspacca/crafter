using Crafter.Core.Projects;

namespace Crafter.Presentation.Capabilities;

// What each capability gives a project's agent, worded for the Capabilities window.
public static class CapabilityCatalog
{
    public static IReadOnlyList<CapabilityInfo> All { get; } =
    [
        new(
            Capability.ManageCrafter,
            "Manages Crafter",
            "The agent can see your projects, rename them, and read and write the skills library.",
            [
                new("list_projects", "Lists the projects, their branches and open chats", Asks: false),
                new("rename_project", "Renames a project", Asks: true),
                new("library_status", "Tells how the skills library stands", Asks: false),
                new("sync_library", "Syncs the skills library now", Asks: false),
                new("list_skills", "Lists the skills in the library", Asks: false),
                new("read_skill", "Reads a skill with its instructions", Asks: false),
                new("save_skill", "Writes a skill, commits and pushes it to the library", Asks: true),
            ]),
    ];
}

public sealed record CapabilityInfo(Capability Capability, string Name, string Description, IReadOnlyList<ToolInfo> Tools);

public sealed record ToolInfo(string Name, string What, bool Asks)
{
    public string Approval => Asks ? "asks first" : "runs unasked";
}
