using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Crafter.Core.Projects;
using Crafter.Desktop.Keyboard;
using Crafter.Desktop.Projects;
using Crafter.Desktop.WindowManagers;

namespace Crafter.Desktop;

public sealed partial class TitleBar : UserControl
{
    private WindowManager windowManager = new Stacking();

    public TitleBar() => InitializeComponent();

    private Window Host => TopLevel.GetTopLevel(this) as Window
        ?? throw new InvalidOperationException("A title bar only lives inside a window.");

    public void ArrangeFor(WindowManager windowManager)
    {
        this.windowManager = windowManager;

        MinimizeButton.IsVisible = windowManager.Minimizes;
        MaximizeButton.IsVisible = windowManager.Maximizes;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not WorkbenchScreen screen)
        {
            return;
        }

        screen.Projects.CollectionChanged += (_, _) => Stock(screen);
        screen.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(WorkbenchScreen.Current))
            {
                Stock(screen);
            }
        };

        Stock(screen);
    }

    public void Follow(WindowState state)
    {
        var maximized = state == WindowState.Maximized;

        MaximizeIcon.IsVisible = !maximized;
        RestoreIcon.IsVisible = maximized;
        AutomationProperties.SetName(MaximizeButton, maximized ? "Restore" : "Maximize");
    }

    private void Drag(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            if (windowManager.Maximizes)
            {
                ToggleMaximized();
            }

            return;
        }

        Host.BeginMoveDrag(e);
    }

    private void Minimize(object? sender, RoutedEventArgs e) => Host.WindowState = WindowState.Minimized;

    private void ToggleMaximize(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void Close(object? sender, RoutedEventArgs e) => Host.Close();

    private void Stock(WorkbenchScreen screen)
    {
        var items = ProjectsMenu.Items;

        items.Clear();
        items.Add(Entry("Open Project…", ProjectShortcuts.Open, screen.SearchCommand));
        items.Add(Entry("Add Project…", ProjectShortcuts.Add, screen.AddCommand));

        if (screen.Projects.Count > 0)
        {
            items.Add(new Separator());
            for (var place = 0; place < screen.Projects.Count; place++)
            {
                items.Add(Shelf(screen, screen.Projects[place], place));
            }
        }

        if (screen.Current is { } current)
        {
            items.Add(new Separator());
            items.Add(Entry($"Remove {current.Name} from Projects", null, screen.RemoveCurrentCommand));
            items.Add(Entry("Close Project", ProjectShortcuts.Close, screen.CloseCommand));
        }
    }

    private static MenuItem Entry(string label, Shortcut? shortcut, ICommand command) => new()
    {
        Header = Row(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }, shortcut),
        Command = command,
    };

    private static MenuItem Shelf(WorkbenchScreen screen, Project project, int place) => new()
    {
        Header = Row(
            new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = project.Name },
                    new TextBlock { Text = new FolderLabel(project.Folder).Text, Classes = { "path" } },
                },
            },
            ProjectShortcuts.Slot(place)),
        Icon = project == screen.Current ? new TextBlock { Text = "●", Classes = { "current" } } : null,
        Command = screen.OpenCommand,
        CommandParameter = project,
    };

    private static Grid Row(Control label, Shortcut? shortcut)
    {
        var keys = new TextBlock
        {
            Text = shortcut?.ToString(),
            Margin = new Thickness(32, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "keys" },
        };
        Grid.SetColumn(keys, 1);

        return new Grid
        {
            MinWidth = 380,
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { label, keys },
        };
    }

    private void ToggleMaximized() =>
        Host.WindowState = Host.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
