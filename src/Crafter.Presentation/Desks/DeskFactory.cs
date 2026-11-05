using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Platform;
using Crafter.Presentation.Projects;

namespace Crafter.Presentation.Desks;

public sealed class DeskFactory(IChatStore store, Crew crew, PreferenceBook preferences, IClipboard clipboard, TimeProvider time)
{
    public ProjectDeskViewModel Make(Project project, ProjectDetailsViewModel? details = null, ProjectAsideViewModel? aside = null, Func<IReadOnlyList<Skill>>? skills = null) =>
        new(project, store, crew, preferences, details, aside, clipboard, time, skills);
}
