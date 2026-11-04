using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Crafter.Desktop.Prose;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Composing;

namespace Crafter.Desktop.Conversations;

public sealed partial class ConversationView : UserControl
{
    private const double Near = 48;

    // One view serves every chat, so each chat keeps where it was left.
    private readonly ConditionalWeakTable<ConversationViewModel, Place> places = new();

    // One selection for the whole chat, across answers, prompts and tool output.
    private readonly TextSelection selection;

    private ConversationViewModel? shown;

    private bool following = true;

    public ConversationView()
    {
        InitializeComponent();
        selection = TextSelection.Watch(Scroller);
        PromptBox.AddHandler(KeyDownEvent, Submit, RoutingStrategies.Tunnel);
        PromptBox.AddHandler(TextBox.PastingFromClipboardEvent, Paste);
        PromptBox.PropertyChanged += Look;
        AddHandler(DragDrop.DragEnterEvent, Hover);
        AddHandler(DragDrop.DragOverEvent, Hover);
        AddHandler(DragDrop.DragLeaveEvent, Leave);
        AddHandler(DragDrop.DropEvent, Drop);
        Composer.AddHandler(Button.ClickEvent, Refocus);
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
            Dropped(viewModel, Local(items));
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

    // Craft, attach and remove buttons hand the keyboard back to the prompt.
    private void Refocus(object? sender, RoutedEventArgs e) => Dispatcher.UIThread.Post(() => PromptBox.Focus());

    // Files, or text that may name them: the drop zone shows while either hovers the chat.
    private void Hover(object? sender, DragEventArgs e)
    {
        var takes = Conversation is not null && (e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text));
        e.DragEffects = takes ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.IsVisible = takes;
    }

    private void Leave(object? sender, DragEventArgs e) => DropZone.IsVisible = false;

    private void Drop(object? sender, DragEventArgs e)
    {
        DropZone.IsVisible = false;
        if (Conversation is not { } viewModel)
        {
            return;
        }

        e.Handled = true;
        PromptBox.Focus();

        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } items)
        {
            Dropped(viewModel, Local(items));
        }
        else if (e.DataTransfer.TryGetText() is { Length: > 0 } text)
        {
            if (DroppedText.Paths(text) is { Count: > 0 } paths)
            {
                Dropped(viewModel, paths);
            }
            else
            {
                Insert(viewModel.Paste(text));
            }
        }
    }

    private static IEnumerable<string> Local(IEnumerable<IStorageItem> items) => items.Select(item => item.TryGetLocalPath()).OfType<string>();

    // Pictures attach as images; any other file or folder attaches as a reference the agent reads.
    private static void Dropped(ConversationViewModel viewModel, IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Pictures.IsPicture(path) && Pictures.Of(path) is { } picture)
            {
                viewModel.Attach(picture);
            }
            else
            {
                viewModel.AttachFile(path);
            }
        }
    }

    private async void Browse(object? sender, RoutedEventArgs e)
    {
        if (Conversation is not { } viewModel || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Attach files", AllowMultiple = true });
        Dropped(viewModel, Local(picked));
        PromptBox.Focus();
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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        selection?.Clear();

        if (shown is not null)
        {
            places.AddOrUpdate(shown, new Place(following, Scroller.Offset.Y));
        }

        shown = Conversation;

        if (shown is null)
        {
            return;
        }

        var chat = shown;
        var place = places.TryGetValue(chat, out var left) ? left : Place.Latest;
        following = place.Following;
        Dispatcher.UIThread.Post(() => Return(chat, place), DispatcherPriority.Loaded);
    }

    private void Return(ConversationViewModel chat, Place place)
    {
        if (shown != chat)
        {
            return;
        }

        if (place.Following)
        {
            Scroller.ScrollToEnd();
        }
        else
        {
            Scroller.Offset = new Vector(Scroller.Offset.X, place.Offset);
        }

        // Opening or switching to a chat puts the cursor in its composer, ready to type.
        PromptBox.Focus();
    }

    private void Follow(object? sender, ScrollChangedEventArgs e)
    {
        var bottom = Scroller.Extent.Height - Scroller.Viewport.Height;
        var resized = e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0;

        // Clamping only moves the offset up when the content shrinks or the viewport grows,
        // so an upward move without either is the reader scrolling away.
        var scrolledUp = e.OffsetDelta.Y < 0 && e.ExtentDelta.Y >= 0 && e.ViewportDelta.Y <= 0;

        if (following && resized && !scrolledUp)
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

    private sealed record Place(bool Following, double Offset)
    {
        public static Place Latest { get; } = new(Following: true, Offset: 0);
    }
}
