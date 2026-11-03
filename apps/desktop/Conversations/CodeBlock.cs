using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Highlight;

namespace Crafter.Desktop.Conversations;

public sealed partial class CodeBlock(string language, Func<string, Task> copy) : AnswerPiece
{
    private readonly Grammar grammar = Grammars.For(language);

    public string Language { get; } = language.Length > 0 ? language : "text";

    [ObservableProperty]
    public partial IReadOnlyList<Token> Tokens { get; set; } = [];

    protected override void Retell(string text) => Tokens = Highlighter.Tokens(text, grammar);

    [RelayCommand]
    private Task Copy() => copy(Text);
}
