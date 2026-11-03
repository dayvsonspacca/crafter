using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Presentation.Conversations;

public sealed partial class PickItem(Pick pick, Action<PickItem> toggle) : ObservableObject
{
    public string Label { get; } = pick.Label;

    public string Description { get; } = pick.Description;

    public bool HasDescription => Description.Length > 0;

    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    [RelayCommand]
    private void Toggle() => toggle(this);
}
