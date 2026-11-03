using Crafter.Core.Agents;

namespace Crafter.Presentation.Conversations;

public sealed class ChatChooser
{
    public ChatChooser(Choice choice, Configuration preferred, Configuration adjustments, Action<string, string?> adjust)
    {
        var chosen = choice.Chosen(adjustments.Over(preferred));
        var usual = choice.Chosen(preferred);

        Title = choice.Title;
        Label = $"{choice.Title} · {chosen.Label}";
        IsChanged = chosen != usual;
        Options =
        [
            .. choice.Options.Select(option => new ChooserOption(
                option == usual ? $"{option.Label} · default" : option.Label,
                option == chosen,
                () => adjust(choice.Key, option == usual ? null : option.Value ?? Configuration.Cleared))),
        ];
    }

    public string Title { get; }

    public string Label { get; }

    public bool IsChanged { get; }

    public IReadOnlyList<ChooserOption> Options { get; }
}
