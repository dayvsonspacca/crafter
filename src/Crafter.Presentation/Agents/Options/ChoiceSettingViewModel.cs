using Crafter.Core.Agents.Options;

namespace Crafter.Presentation.Agents.Options;

public sealed class ChoiceSettingViewModel
{
    private readonly Choice choice;

    private readonly Action<string, string?> change;

    public ChoiceSettingViewModel(Choice choice, Configuration configuration, Action<string, string?> change)
    {
        this.choice = choice;
        this.change = change;

        var chosen = choice.Chosen(configuration);
        Options = [.. choice.Options.Select(option => new ChoiceOptionViewModel(option, option == chosen, Pick))];
    }

    public string Title => choice.Title;

    public string About => choice.About;

    public IReadOnlyList<ChoiceOptionViewModel> Options { get; }

    public ChoiceOptionViewModel Chosen
    {
        get => Options.First(option => option.IsChosen);
        set
        {
            if (value is { IsChosen: false })
            {
                Pick(value);
            }
        }
    }

    private void Pick(ChoiceOptionViewModel picked)
    {
        foreach (var option in Options)
        {
            option.IsChosen = option == picked;
        }

        change(choice.Key, picked.Option.Value);
    }
}
