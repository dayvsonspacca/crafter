namespace Crafter.Core.Library;

// A piece of know-how from the library: a folder under skills/ with what it is for and how to do it.
public sealed record Skill(string Id, string Name, string Description, string? WhenToUse, string Folder)
{
    public string Instructions => Path.Combine(Folder, "instructions.md");
}
