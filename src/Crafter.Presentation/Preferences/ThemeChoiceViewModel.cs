using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Presentation.Themes;

namespace Crafter.Presentation.Preferences;

public sealed partial class ThemeChoiceViewModel(Theme theme, bool isChosen, Action<ThemeChoiceViewModel> choose) : ObservableObject
{
    public Theme Theme { get; } = theme;

    [ObservableProperty]
    public partial bool IsChosen { get; set; } = isChosen;

    [RelayCommand]
    private void Choose() => choose(this);
}
