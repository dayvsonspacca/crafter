using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Crafter.Highlight;

namespace Crafter.Desktop.Conversations;

public sealed class HighlightedCode : SelectableTextBlock
{
    public static readonly StyledProperty<IReadOnlyList<Token>> TokensProperty =
        AvaloniaProperty.Register<HighlightedCode, IReadOnlyList<Token>>(nameof(Tokens), []);

    private static readonly Dictionary<Shade, string> Brushes = new()
    {
        [Shade.Keyword] = "AccentBrush",
        [Shade.Type] = "RemovedBrush",
        [Shade.Text] = "AccentSecondaryBrush",
        [Shade.Number] = "AddedBrush",
        [Shade.Comment] = "ForegroundMutedBrush",
        [Shade.Attribute] = "AccentSecondaryBrush",
    };

    public IReadOnlyList<Token> Tokens
    {
        get => GetValue(TokensProperty);
        set => SetValue(TokensProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TokensProperty)
        {
            Retell();
        }
    }

    private void Retell()
    {
        var inlines = new InlineCollection();

        foreach (var token in Tokens)
        {
            var run = new Run(token.Text);
            if (Brushes.TryGetValue(token.Shade, out var brush))
            {
                run.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(brush));
            }

            if (token.Shade == Shade.Comment)
            {
                run.FontStyle = FontStyle.Italic;
            }

            inlines.Add(run);
        }

        Inlines = inlines;
    }
}
