using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Desktop.Tests;

internal sealed class ArchiveInMemory : IChatArchive
{
    public IReadOnlyList<ChatRecord> Saved { get; private set; } = [];

    public IReadOnlyList<ChatRecord> Load(Project project) => Saved;

    public void Save(Project project, IReadOnlyList<ChatRecord> chats) => Saved = [.. chats];
}
