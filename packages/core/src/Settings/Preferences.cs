using Crafter.Core.Agents;

namespace Crafter.Core.Settings;

public sealed record Preferences
{
    public const int ShallowestSearch = 1;

    public const int DeepestSearch = 8;

    private readonly string projectsRoot = string.Empty;

    private readonly int searchDepth;

    private readonly IReadOnlyList<AgentId> agents = [];

    public Preferences(string projectsRoot, bool findsRepositories, int searchDepth)
    {
        ProjectsRoot = projectsRoot;
        FindsRepositories = findsRepositories;
        SearchDepth = searchDepth;
    }

    public string ProjectsRoot
    {
        get => projectsRoot;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            projectsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        }
    }

    public bool FindsRepositories { get; init; }

    public int SearchDepth
    {
        get => searchDepth;
        init => searchDepth = Math.Clamp(value, ShallowestSearch, DeepestSearch);
    }

    public IReadOnlyList<AgentId> Agents
    {
        get => agents;
        init => agents = [.. value.Distinct()];
    }

    public static Preferences Default(string home) => new(home, findsRepositories: true, searchDepth: 4);

    public bool Equals(Preferences? other) =>
        other is not null
        && ProjectsRoot == other.ProjectsRoot
        && FindsRepositories == other.FindsRepositories
        && SearchDepth == other.SearchDepth
        && Agents.SequenceEqual(other.Agents);

    public override int GetHashCode() => HashCode.Combine(ProjectsRoot, FindsRepositories, SearchDepth, Agents.Count);
}
