using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Presentation.Themes;

namespace Crafter.Presentation.Preferences;

public sealed partial class ThemeChoiceViewModel(Theme theme, bool isChosen, Appearance appearance, Action<ThemeChoiceViewModel> choose) : ObservableObject
{
    public Theme Theme { get; } = theme;

    public ThemePalette Palette => Theme.In(Appearance);

    [ObservableProperty]
    public partial bool IsChosen { get; set; } = isChosen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Palette))]
    public partial Appearance Appearance { get; set; } = appearance;

    [RelayCommand]
    private void Choose() => choose(this);
}
