using CommunityToolkit.Mvvm.ComponentModel;

namespace Crafter.Presentation.Conversations.Messages;

public abstract partial class MessageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLast { get; set; }
}
