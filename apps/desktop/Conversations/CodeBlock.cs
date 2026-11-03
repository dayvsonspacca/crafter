using CommunityToolkit.Mvvm.Input;

namespace Crafter.Desktop.Conversations;

public sealed partial class CodeBlock(string language, Func<string, Task> copy) : AnswerPiece
{
    public string Language { get; } = language.Length > 0 ? language : "text";

    [RelayCommand]
    private Task Copy() => copy(Text);
}
