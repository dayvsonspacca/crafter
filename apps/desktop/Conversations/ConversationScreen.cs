using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations;

namespace Crafter.Desktop.Conversations;

public sealed partial class ConversationScreen : ObservableObject
{
    private readonly Conversation conversation;

    public ConversationScreen(Conversation conversation)
    {
        this.conversation = conversation;
        Draft = string.Empty;
        Model = "Claude Code";
        IsEmpty = true;
    }

    public ObservableCollection<Message> Messages { get; } = [];

    [ObservableProperty]
    public partial string Draft { get; set; }

    [ObservableProperty]
    public partial string Model { get; set; }

    [ObservableProperty]
    public partial bool IsCrafting { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Craft(CancellationToken stop)
    {
        if (string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var prompt = new Prompt(Draft.Trim());
        var answer = new CrafterMessage();

        Draft = string.Empty;
        Messages.Add(new YourMessage(prompt.Text));
        Messages.Add(answer);
        IsEmpty = false;
        IsCrafting = true;

        await foreach (var reply in conversation.Ask(prompt, stop))
        {
            if (reply is Began began)
            {
                Model = new ModelName(began.Model).ToString();
            }

            answer.Hear(reply);
        }

        IsCrafting = false;
    }
}
