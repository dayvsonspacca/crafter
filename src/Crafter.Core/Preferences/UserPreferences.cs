using Crafter.Core.Agents;
using Crafter.Core.Agents.Options;

namespace Crafter.Core.Preferences;

public sealed record UserPreferences
{
    private readonly string projectsRoot = string.Empty;

    private readonly IReadOnlyList<AgentId> agents = [];

    private readonly IReadOnlyList<string> defaultSkills = [];

    private readonly IReadOnlyDictionary<AgentId, Configuration> configurations = new Dictionary<AgentId, Configuration>();

    public UserPreferences(string projectsRoot)
    {
        ProjectsRoot = projectsRoot;
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

    public IReadOnlyList<AgentId> Agents
    {
        get => agents;
        init => agents = [.. (value ?? []).Distinct()];
    }

    public string? Theme
    {
        get;
        init => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // The repository that keeps the skills and agents, as git can clone it.
    public string? Library
    {
        get;
        init => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // The library skills every project gets, by id. This Crafter's choice: another machine linked
    // to the same library keeps its own.
    public IReadOnlyList<string> DefaultSkills
    {
        get => defaultSkills;
        init => defaultSkills = [.. (value ?? []).Distinct()];
    }

    // Whether the system looked dark the last time Crafter saw it, so the next start can paint
    // the right variant before the system answers.
    public bool? SystemDark { get; init; }

    public IReadOnlyDictionary<AgentId, Configuration> Configurations
    {
        get => configurations;
        init => configurations = (value ?? new Dictionary<AgentId, Configuration>()).Where(each => each.Value.Values.Count > 0).ToDictionary();
    }

    public static UserPreferences Default(string home) => new(home);

    public Configuration ConfigurationOf(AgentId agent) => configurations.GetValueOrDefault(agent) ?? Configuration.Empty;

    public UserPreferences Configure(AgentId agent, Configuration configuration) =>
        this with { Configurations = new Dictionary<AgentId, Configuration>(configurations) { [agent] = configuration } };

    public bool Equals(UserPreferences? other) =>
        other is not null
        && ProjectsRoot == other.ProjectsRoot
        && Theme == other.Theme
        && SystemDark == other.SystemDark
        && Library == other.Library
        && Agents.SequenceEqual(other.Agents)
        && DefaultSkills.SequenceEqual(other.DefaultSkills)
        && configurations.Count == other.configurations.Count
        && configurations.All(each => other.ConfigurationOf(each.Key).Equals(each.Value));

    public override int GetHashCode() => HashCode.Combine(ProjectsRoot, Theme, SystemDark, Library, Agents.Count, DefaultSkills.Count, configurations.Count);
}
