namespace Crafter.Presentation.Conversations;

public interface IChatKeeper
{
    void Select(ConversationViewModel chat);

    void Close(ConversationViewModel chat);

    void Keep(ConversationViewModel chat);
}
