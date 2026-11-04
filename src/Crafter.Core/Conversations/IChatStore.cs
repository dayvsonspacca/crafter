using Crafter.Core.Projects;

namespace Crafter.Core.Conversations;

public interface IChatStore
{
    IReadOnlyList<ChatRecord> Load(Project project);

    void Save(Project project, IReadOnlyList<ChatRecord> chats);
}
