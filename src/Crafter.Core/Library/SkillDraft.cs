namespace Crafter.Core.Library;

// A skill to write into the library, new or replacing the one with the same id.
public sealed record SkillDraft(string Id, string Name, string Description, string? WhenToUse, string Instructions);
