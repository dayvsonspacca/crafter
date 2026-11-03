using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Crafter.Desktop.Projects;

public sealed partial class ProjectsSidebar : UserControl
{
    public ProjectsSidebar() => InitializeComponent();

    private void ShowMenu(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is Control more && more.FindAncestorOfType<Button>() is { ContextFlyout: { } menu } project)
        {
            menu.ShowAt(project);
        }
    }
}
