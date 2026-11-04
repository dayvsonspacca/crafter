using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Preferences;
using Crafter.Core.Projects;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Desks;

public sealed class DeskFactory(IChatArchive archive, Crew crew, PreferenceBook preferences, IClipboard clipboard, TimeProvider time)
{
    public ProjectDesk Make(Project project, Func<Project, Task> edit) => new(project, archive, crew, preferences, edit, clipboard, time);
}
