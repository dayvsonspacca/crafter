using CommunityToolkit.Mvvm.ComponentModel;

namespace Crafter.Desktop.Conversations;

public abstract partial class AnswerPiece : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    partial void OnTextChanged(string value) => Retell(value);

    protected virtual void Retell(string text)
    {
    }
}
