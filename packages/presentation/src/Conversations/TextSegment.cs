using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Conversations;

public sealed partial class TextSegment(IClipboard clipboard) : ObservableObject
{
    public ObservableCollection<AnswerPiece> Pieces { get; } = [];

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

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

            AnswerPiece piece = language is null ? new Prose() : new CodeBlock(language, clipboard);
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

    private static bool Fits(AnswerPiece piece, string? language) => piece switch
    {
        Prose => language is null,
        CodeBlock code => language is not null && code.Language == (language.Length > 0 ? language : "text"),
        _ => false,
    };
}
