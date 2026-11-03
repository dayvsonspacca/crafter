namespace Crafter.Desktop.Conversations;

public interface IChatKeeper
{
    void Select(ConversationScreen chat);

    void Close(ConversationScreen chat);
}
