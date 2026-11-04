using Crafter.Core.Conversations.Replies;

namespace Crafter.Core.Agents.Background;

public interface IWorksInBackground
{
    event EventHandler<IReadOnlyList<Job>>? JobsChanged;

    event EventHandler<IAsyncEnumerable<Reply>>? Woke;

    IReadOnlyList<Job> Jobs { get; }

    // The latest of what a job printed, or nothing when it has not printed or cannot be read.
    string? Output(string job);

    void Stop(string job);

    void Dismiss();
}
