namespace Crafter.Desktop.Conversations;

public interface IAwaitsDecision
{
    string Id { get; }

    bool IsOpen { get; }

    void Close(string verdict);
}
