using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Crafter.Desktop;

public sealed class Modal : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<Modal, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> IsCompactProperty = AvaloniaProperty.Register<Modal, bool>(nameof(IsCompact));

    public static readonly RoutedEvent<RoutedEventArgs> DismissedEvent =
        RoutedEvent.Register<Modal, RoutedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    private Control? backdrop;

    private Button? close;

    static Modal() => IsCompactProperty.Changed.AddClassHandler<Modal>((modal, _) => modal.PseudoClasses.Set(":compact", modal.IsCompact));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (backdrop is not null)
        {
            backdrop.PointerPressed -= Dismiss;
        }

        if (close is not null)
        {
            close.Click -= Dismiss;
        }

        backdrop = e.NameScope.Find<Control>("PART_Backdrop");
        close = e.NameScope.Find<Button>("PART_Close");

        if (backdrop is not null)
        {
            backdrop.PointerPressed += Dismiss;
        }

        if (close is not null)
        {
            close.Click += Dismiss;
        }
    }

    private void Dismiss(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(DismissedEvent, this));
    }
}
