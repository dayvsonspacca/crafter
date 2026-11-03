using Crafter.Core.Projects;

namespace Crafter.Core.Conversations;

public interface IChatArchive
{
    IReadOnlyList<ChatRecord> Load(Project project);

    void Save(Project project, IReadOnlyList<ChatRecord> chats);
}
