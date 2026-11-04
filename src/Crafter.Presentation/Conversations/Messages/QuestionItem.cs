using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Presentation.Conversations.Messages;

public sealed partial class QuestionItem : ObservableObject, IAwaitsDecision
{
    private readonly Action<IAwaitsDecision, Decision> decide;

    public QuestionItem(NeedsAnswers asked, string agent, Action<IAwaitsDecision, Decision> decide)
    {
        this.decide = decide;
        Id = asked.Id;
        Asking = asked.Questions.Count == 1 ? $"{agent} has a question" : $"{agent} has {asked.Questions.Count} questions";
        Questions = [.. asked.Questions.Select(question => new QuestionCard(question, Changed))];
    }

    public string Id { get; }

    public string Asking { get; }

    public IReadOnlyList<QuestionCard> Questions { get; }

    public bool IsOpen => Verdict is null;

    public bool CanSubmit => IsOpen && Questions.All(question => question.IsAnswered);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(CanSubmit))]
    public partial string? Verdict { get; set; }

    public void Close(string verdict)
    {
        if (IsOpen)
        {
            Verdict = verdict;
            Lock();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private void Submit() =>
        Decide(new Answers(Questions.ToDictionary(question => question.Text, question => question.Answer)), "Answered");

    [RelayCommand]
    private void Skip() => Decide(new Deny(), "Skipped");

    private void Decide(Decision decision, string verdict)
    {
        if (IsOpen)
        {
            Verdict = verdict;
            Lock();
            decide(this, decision);
        }
    }

    private void Lock()
    {
        foreach (var question in Questions)
        {
            question.IsOpen = false;
        }
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(CanSubmit));
        SubmitCommand.NotifyCanExecuteChanged();
    }
}
