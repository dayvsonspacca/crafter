using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Presentation.Conversations.Messages;

public sealed partial class QuestionCardViewModel : ObservableObject
{
    private readonly Action changed;

    public QuestionCardViewModel(Question question, Action changed)
    {
        this.changed = changed;
        Text = question.Text;
        Header = question.Header;
        Several = question.Several;
        Picks = [.. question.Picks.Select(pick => new PickItemViewModel(pick, Toggle))];
    }

    public string Text { get; }

    public string Header { get; }

    public bool HasHeader => Header.Length > 0;

    public bool Several { get; }

    public string Hint => Several ? "Pick any that apply, or write your own" : "Pick one, or write your own";

    public IReadOnlyList<PickItemViewModel> Picks { get; }

    public bool IsAnswered => Answer.Length > 0;

    public string Answer =>
        string.Join(", ", Picks.Where(pick => pick.IsChosen).Select(pick => pick.Label).Append(Other?.Trim() ?? string.Empty).Where(part => part.Length > 0));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Answer), nameof(IsAnswered))]
    public partial string? Other { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; } = true;

    partial void OnOtherChanged(string? value)
    {
        if (!Several && !string.IsNullOrWhiteSpace(value))
        {
            foreach (var pick in Picks)
            {
                pick.IsChosen = false;
            }
        }

        changed();
    }

    private void Toggle(PickItemViewModel toggled)
    {
        if (!IsOpen)
        {
            return;
        }

        var choosing = !toggled.IsChosen;
        if (!Several)
        {
            foreach (var pick in Picks)
            {
                pick.IsChosen = false;
            }

            if (choosing)
            {
                Other = null;
            }
        }

        toggled.IsChosen = choosing;
        OnPropertyChanged(nameof(Answer));
        OnPropertyChanged(nameof(IsAnswered));
        changed();
    }
}
