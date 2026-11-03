using Crafter.Core.Agents;
using Crafter.Core.Conversations;
using Crafter.Core.Projects;
using Crafter.Desktop.Platform;

namespace Crafter.Desktop.Conversations;

public sealed class DeskFactory(IChatArchive archive, Crew crew, IClipboard clipboard, TimeProvider time)
{
    public ProjectDesk Make(Project project, Func<Project, Task> edit) => new(project, archive, crew, edit, clipboard, time);
}
