namespace Crafter.Core.Projects;

public sealed record Shelved(Project Project, DateTimeOffset LastOpened)
{
    public IReadOnlyList<Capability> Capabilities { get; init; } = [];

    // Library skills given to this project on top of the defaults, by id.
    public IReadOnlyList<string> Skills { get; init; } = [];

    public bool Equals(Shelved? other) =>
        other is not null && Project == other.Project && LastOpened == other.LastOpened && Capabilities.SequenceEqual(other.Capabilities) && Skills.SequenceEqual(other.Skills);

    public override int GetHashCode() => HashCode.Combine(Project, LastOpened, Capabilities.Count, Skills.Count);
}
