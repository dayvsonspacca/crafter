namespace Crafter.Core.Projects;

// A power a project's agent is given beyond the project itself. Granted per project in Crafter's
// own list, never in the project's files, so a cloned repository cannot grant itself one.
public enum Capability
{
    // See your projects, rename them, and read and write the skills library.
    ManageCrafter,
}

public static class Capabilities
{
    public static string Id(Capability capability) => capability switch
    {
        Capability.ManageCrafter => "manage-crafter",
        _ => throw new ArgumentOutOfRangeException(nameof(capability)),
    };

    public static Capability? Of(string id) => id switch
    {
        "manage-crafter" => Capability.ManageCrafter,
        _ => null,
    };
}
