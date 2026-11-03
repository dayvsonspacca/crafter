namespace Crafter.Presentation.Conversations;

public interface IChatKeeper
{
    void Select(ConversationScreen chat);

    void Close(ConversationScreen chat);

    void Keep(ConversationScreen chat);
}
