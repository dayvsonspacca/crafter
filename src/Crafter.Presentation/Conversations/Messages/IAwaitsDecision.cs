namespace Crafter.Presentation.Conversations.Messages;

public interface IAwaitsDecision
{
    string Id { get; }

    bool IsOpen { get; }

    void Close(string verdict);
}
