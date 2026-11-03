namespace Crafter.Core.Conversations;

public sealed record Prompt
{
    public Prompt(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    public string Text { get; }
}
