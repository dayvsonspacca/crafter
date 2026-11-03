using Crafter.Core.Agents;

namespace Crafter.Desktop.Agents;

public sealed class ChoiceSetting
{
    private readonly Choice choice;

    private readonly Action<string, string?> change;

    public ChoiceSetting(Choice choice, Configuration configuration, Action<string, string?> change)
    {
        this.choice = choice;
        this.change = change;

        var chosen = choice.Chosen(configuration);
        Options = [.. choice.Options.Select(option => new ChoiceOption(option, option == chosen, Pick))];
    }

    public string Title => choice.Title;

    public string About => choice.About;

    public IReadOnlyList<ChoiceOption> Options { get; }

    private void Pick(ChoiceOption picked)
    {
        foreach (var option in Options)
        {
            option.IsChosen = option == picked;
        }

        change(choice.Key, picked.Option.Value);
    }
}
