namespace Crafter.Core.Projects;

// What a project keeps about itself in its own .crafter folder, so it travels with it: its name, its
// icon, and its axioms, the rules its agent follows in every chat.
public sealed record ProjectSettings(ProjectName Name, string? Icon = null)
{
    public IReadOnlyList<string> Axioms { get; init; } = [];

    public bool Equals(ProjectSettings? other) =>
        other is not null && Name == other.Name && Icon == other.Icon && Axioms.SequenceEqual(other.Axioms);

    public override int GetHashCode() => HashCode.Combine(Name, Icon, Axioms.Count);
}
