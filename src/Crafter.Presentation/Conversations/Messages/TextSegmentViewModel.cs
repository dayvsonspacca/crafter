using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Conversations.Messages;

public sealed partial class TextSegmentViewModel(IClipboard clipboard) : ObservableObject
{
    public ObservableCollection<AnswerPieceViewModel> Pieces { get; } = [];

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    partial void OnTextChanged(string value)
    {
        var pieces = AnswerPieces.Of(value);

        for (var at = 0; at < pieces.Count; at++)
        {
            var (language, text) = pieces[at];
            if (at < Pieces.Count && Fits(Pieces[at], language))
            {
                Pieces[at].Text = text;
                continue;
            }

            AnswerPieceViewModel piece = language is null ? new ProseViewModel() : new CodeBlockViewModel(language, clipboard);
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

    private static bool Fits(AnswerPieceViewModel piece, string? language) => piece switch
    {
        ProseViewModel => language is null,
        CodeBlockViewModel code => language is not null && code.Language == (language.Length > 0 ? language : "text"),
        _ => false,
    };
}
