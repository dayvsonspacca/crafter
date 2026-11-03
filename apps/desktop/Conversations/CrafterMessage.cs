using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class CrafterMessage : Message
{
    public CrafterMessage()
    {
        Text = string.Empty;
        IsCrafting = true;
    }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial bool IsCrafting { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial bool HasFailed { get; set; }

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

    private static string? NoteFor(Ending ending) => ending switch
    {
        Stopped => "Stopped.",
        CutShort => "Cut short: the answer reached its length limit.",
        Refused { Category: { } category } => $"Claude declined to answer ({category}).",
        Refused => "Claude declined to answer.",
        Failed failed => failed.Reason,
        _ => null,
    };
}
