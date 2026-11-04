namespace Crafter.Core.Projects;

public sealed record Shelved(Project Project, DateTimeOffset LastOpened)
{
    // Library skills given to this project on top of the defaults, by id.
    public IReadOnlyList<string> Skills { get; init; } = [];

    // The project's axioms: rules its agent follows in every chat, in the order they were set.
    public IReadOnlyList<string> Axioms { get; init; } = [];

    public bool Equals(Shelved? other) =>
        other is not null && Project == other.Project && LastOpened == other.LastOpened && Skills.SequenceEqual(other.Skills) && Axioms.SequenceEqual(other.Axioms);

    public override int GetHashCode() => HashCode.Combine(Project, LastOpened, Skills.Count, Axioms.Count);
}
