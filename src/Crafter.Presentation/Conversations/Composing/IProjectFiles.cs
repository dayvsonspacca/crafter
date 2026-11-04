namespace Crafter.Presentation.Conversations.Composing;

public interface IProjectFiles
{
    string Folder { get; }

    Task<IReadOnlyList<string>> List(CancellationToken stop = default);
}
