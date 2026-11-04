using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Agents.Options;

namespace Crafter.Presentation.Agents.Options;

public sealed partial class ChoiceOptionViewModel(Option option, bool isChosen, Action<ChoiceOptionViewModel> pick) : ObservableObject
{
    public Option Option { get; } = option;

    public string Label => Option.Label;

    [ObservableProperty]
    public partial bool IsChosen { get; set; } = isChosen;

    [RelayCommand]
    private void Choose() => pick(this);
}
