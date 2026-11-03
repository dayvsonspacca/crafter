using CommunityToolkit.Mvvm.ComponentModel;

namespace Crafter.Desktop.Conversations;

public abstract partial class Message : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLast { get; set; }
}
