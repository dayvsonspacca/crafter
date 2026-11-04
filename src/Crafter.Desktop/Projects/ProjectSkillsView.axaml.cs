using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Crafter.Presentation.Projects;

namespace Crafter.Desktop.Projects;

public sealed partial class ProjectSkillsView : UserControl
{
    public ProjectSkillsView()
    {
        InitializeComponent();
    }

    private ProjectSkillsViewModel? Skills => DataContext as ProjectSkillsViewModel;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Taking the focus lets Escape close the window.
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Dispatcher.UIThread.Post(() => Focus());
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Skills?.Close();
        }
    }

    private void Dismiss(object? sender, RoutedEventArgs e) => Skills?.Close();
}
