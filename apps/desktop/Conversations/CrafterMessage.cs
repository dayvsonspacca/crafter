using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class CrafterMessage : Message
{
    private readonly Func<string, Task> copy;

    private readonly Action<CrafterMessage> retry;

    public CrafterMessage(Func<string, Task> copy, Action<CrafterMessage> retry)
    {
        this.copy = copy;
        this.retry = retry;
        Text = string.Empty;
        IsCrafting = true;
    }

    public CrafterMessage(string text, Ending ending, Func<string, Task> copy, Action<CrafterMessage> retry)
        : this(copy, retry)
    {
        Text = text;
        Hear(ending);
    }

    public ObservableCollection<AnswerPiece> Pieces { get; } = [];

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    public partial bool IsCrafting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote), nameof(IsDone))]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial bool HasFailed { get; set; }

    public bool HasNote => Note is not null;

    public bool IsDone => !IsCrafting && Note is null;

    public void Hear(Reply reply)
    {
        switch (reply)
        {
            case Wrote wrote:
                Text += wrote.Fragment;
                break;
            case Ending ending:
                IsCrafting = false;
                HasFailed = ending is Failed;
                Note = NoteFor(ending);
                break;
        }
    }

    partial void OnTextChanged(string value)
    {
        var pieces = Conversations.Pieces.Of(value);

        for (var at = 0; at < pieces.Count; at++)
        {
            var (language, text) = pieces[at];
            if (at < Pieces.Count && Fits(Pieces[at], language))
            {
                Pieces[at].Text = text;
                continue;
            }

            AnswerPiece piece = language is null ? new Prose() : new CodeBlock(language, copy);
            piece.Text = text;

            if (at < Pieces.Count)
            {
                Pieces[at] = piece;
            }
            else
            {
                Pieces.Add(piece);
            }
        }

        while (Pieces.Count > pieces.Count)
        {
            Pieces.RemoveAt(Pieces.Count - 1);
        }
    }

    [RelayCommand]
    private Task Copy() => copy(Text);

    [RelayCommand]
    private void Retry() => retry(this);

    private static bool Fits(AnswerPiece piece, string? language) => piece switch
    {
        Prose => language is null,
        CodeBlock code => language is not null && code.Language == (language.Length > 0 ? language : "text"),
        _ => false,
    };

    private static string? NoteFor(Ending ending) => ending switch
    {
        Stopped => "Stopped",
        CutShort => "Cut short at the length limit",
        Refused { Category: { } category } => $"Declined ({category})",
        Refused => "Declined",
        Failed failed => failed.Reason,
        _ => null,
    };
}
