using CommunityToolkit.Mvvm.ComponentModel;

namespace Crafter.Presentation.Conversations;

public abstract partial class Message : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLast { get; set; }
}
