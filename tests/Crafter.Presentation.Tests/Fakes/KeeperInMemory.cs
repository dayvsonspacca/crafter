using Crafter.Presentation.Conversations;

namespace Crafter.Presentation.Tests.Fakes;

internal sealed class KeeperInMemory : IChatKeeper
{
    public void Select(ConversationViewModel chat)
    {
    }

    public void Close(ConversationViewModel chat)
    {
    }

    public void Keep(ConversationViewModel chat)
    {
    }

    public int PanelToggles { get; private set; }

    public void TogglePanel() => PanelToggles++;
}
