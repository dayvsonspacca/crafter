using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Composing;

namespace Crafter.Desktop.Conversations;

public sealed partial class ConversationView : UserControl
{
    private const double Near = 48;

    private bool following = true;

    public ConversationView()
    {
        InitializeComponent();
        PromptBox.AddHandler(KeyDownEvent, Submit, RoutingStrategies.Tunnel);
        PromptBox.AddHandler(TextBox.PastingFromClipboardEvent, Paste);
        PromptBox.PropertyChanged += Look;
        Composer.AddHandler(DragDrop.DragOverEvent, Hover);
        Composer.AddHandler(DragDrop.DropEvent, Drop);
    }

    private ConversationViewModel? Conversation => DataContext as ConversationViewModel;

    private MentionsViewModel? Mentions => Conversation?.Mentions;

    private void Submit(object? sender, KeyEventArgs e)
    {
        if (Mentions is { IsOpen: true } mentions && Steer(mentions, e))
        {
            e.Handled = true;

            return;
        }

        if (e.Key == Key.Back && PromptBox.CaretIndex == 0 && PromptBox.SelectionStart == PromptBox.SelectionEnd && Conversation is { HasAttached: true } attached)
        {
            e.Handled = true;
            attached.DetachLast();

            return;
        }

        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;

        if (Conversation is { } viewModel && viewModel.CraftCommand.CanExecute(null))
        {
            following = true;
            viewModel.CraftCommand.Execute(null);
        }
    }

    private bool Steer(MentionsViewModel mentions, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                mentions.Move(-1);
                return true;
            case Key.Down:
                mentions.Move(1);
                return true;
            case Key.Escape:
                mentions.Close();
                return true;
            case Key.Tab or Key.Enter when e.KeyModifiers == KeyModifiers.None:
                Choose(mentions, chosen: null);
                return true;
            default:
                return false;
        }
    }

    private void Choose(MentionsViewModel mentions, MentionViewModel? chosen)
    {
        if (mentions.Accept(PromptBox.Text ?? string.Empty, chosen) is not { } accepted)
        {
            return;
        }

        PromptBox.Text = accepted.Draft;
        PromptBox.CaretIndex = accepted.Caret;
        PromptBox.ClearSelection();
        PromptBox.Focus();
    }

    private void Mention(object? sender, RoutedEventArgs e)
    {
        if (Mentions is { } mentions && sender is Control { DataContext: MentionViewModel chosen })
        {
            Choose(mentions, chosen);
        }
    }

    private void Look(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty)
        {
            Mentions?.Look(PromptBox.Text ?? string.Empty, PromptBox.CaretIndex);
        }
    }

    private async void Paste(object? sender, RoutedEventArgs e)
    {
        if (Conversation is not { } viewModel || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        e.Handled = true;

        if (await clipboard.TryGetFilesAsync() is { Length: > 0 } items)
        {
            Insert(Dropped(viewModel, items));
        }
        else if (await clipboard.TryGetTextAsync() is { Length: > 0 } text)
        {
            Insert(viewModel.Paste(text));
        }
        else if (await clipboard.TryGetBitmapAsync() is { } bitmap)
        {
            using (bitmap)
            {
                if (Pictures.Of(bitmap) is { } picture)
                {
                    viewModel.Attach(picture);
                }
            }
        }
    }

    private void Hover(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Drop(object? sender, DragEventArgs e)
    {
        if (Conversation is { } viewModel && e.DataTransfer.TryGetFiles() is { Length: > 0 } items)
        {
            e.Handled = true;
            PromptBox.Focus();
            Insert(Dropped(viewModel, items));
        }
    }

    private static string Dropped(ConversationViewModel viewModel, IEnumerable<IStorageItem> items)
    {
        var mentioned = string.Empty;
        foreach (var path in items.Select(item => item.TryGetLocalPath()).OfType<string>())
        {
            if (Pictures.IsPicture(path) && Pictures.Of(path) is { } picture)
            {
                viewModel.Attach(picture);
            }
            else
            {
                mentioned += viewModel.Mention(path);
            }
        }

        return mentioned;
    }

    private void Insert(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        var current = PromptBox.Text ?? string.Empty;
        var from = Math.Clamp(Math.Min(PromptBox.SelectionStart, PromptBox.SelectionEnd), 0, current.Length);
        var to = Math.Clamp(Math.Max(PromptBox.SelectionStart, PromptBox.SelectionEnd), from, current.Length);

        PromptBox.Text = current[..from] + text + current[to..];
        PromptBox.CaretIndex = from + text.Length;
        PromptBox.ClearSelection();
    }

    private void Follow(object? sender, ScrollChangedEventArgs e)
    {
        var bottom = Scroller.Extent.Height - Scroller.Viewport.Height;

        if (e.ExtentDelta.Y > 0 && following)
        {
            Scroller.ScrollToEnd();
        }
        else if (e.OffsetDelta.Y != 0)
        {
            following = Scroller.Offset.Y >= bottom - Near;
        }

        Jump.IsVisible = !following && bottom > Near;
    }

    private void ToLatest(object? sender, RoutedEventArgs e)
    {
        following = true;
        Scroller.ScrollToEnd();
    }
}
