using CommunityToolkit.Mvvm.ComponentModel;

namespace Crafter.Desktop.Conversations;

public abstract partial class AnswerPiece : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;
}
