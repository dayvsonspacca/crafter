namespace Crafter.Core.Projects;

public sealed record Shelved(Project Project, DateTimeOffset LastOpened)
{
    public IReadOnlyList<Capability> Capabilities { get; init; } = [];

    public bool Equals(Shelved? other) =>
        other is not null && Project == other.Project && LastOpened == other.LastOpened && Capabilities.SequenceEqual(other.Capabilities);

    public override int GetHashCode() => HashCode.Combine(Project, LastOpened, Capabilities.Count);
}
