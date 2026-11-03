using CommunityToolkit.Mvvm.Input;

namespace Crafter.Presentation.Conversations;

public sealed partial class ChooserOption(string label, bool isChosen, Action choose)
{
    public string Label { get; } = label;

    public bool IsChosen { get; } = isChosen;

    [RelayCommand]
    private void Choose() => choose();
}
