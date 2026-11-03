using Crafter.Core.Conversations;
using Crafter.Core.Projects;

namespace Crafter.Core.Agents;

public interface IKeepsHistory
{
    Task<Recollection> History(Project project, int latest, CancellationToken stop = default);

    Task<IReadOnlyList<Turn>> Replay(Project project, string sessionId, CancellationToken stop = default);
}
