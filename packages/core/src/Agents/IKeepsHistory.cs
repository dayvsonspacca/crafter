using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Core.Agents;

public interface IKeepsHistory
{
    Task<IReadOnlyList<PastChat>> History(Project project, CancellationToken stop = default);

    Task<IReadOnlyList<Turn>> Replay(Project project, string sessionId, CancellationToken stop = default);
}
