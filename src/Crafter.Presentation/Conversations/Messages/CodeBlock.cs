using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Highlight;
using Crafter.Presentation.Platform;

namespace Crafter.Presentation.Conversations.Messages;

public sealed partial class CodeBlock(string language, IClipboard clipboard) : AnswerPiece
{
    private readonly Grammar grammar = Grammars.For(language);

    public string Language { get; } = language.Length > 0 ? language : "text";

    [ObservableProperty]
    public partial IReadOnlyList<Token> Tokens { get; set; } = [];

    protected override void Retell(string text) => Tokens = Highlighter.Tokens(text, grammar);

    [RelayCommand]
    private Task Copy() => clipboard.Copy(Text);
}
