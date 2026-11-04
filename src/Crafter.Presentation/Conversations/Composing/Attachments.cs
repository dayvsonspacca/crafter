using System.Text.RegularExpressions;
using Crafter.Core.Conversations;

namespace Crafter.Presentation.Conversations.Composing;

public sealed partial class Attachments
{
    public const int LongestPaste = 800;

    public const int MostPastedLines = 8;

    private readonly Dictionary<int, string> pasted = [];

    private readonly List<Picture> pictures = [];

    private int last;

    public IReadOnlyList<Picture> Pictures => pictures;

    public string Paste(string text)
    {
        var flat = text.ReplaceLineEndings("\n");
        var breaks = flat.TrimEnd('\n').Count(letter => letter == '\n');
        if (flat.Length <= LongestPaste && breaks < MostPastedLines)
        {
            return flat;
        }

        pasted[++last] = flat;

        return breaks > 0 ? $"[Pasted text #{last} +{breaks} lines]" : $"[Pasted text #{last}]";
    }

    public void Attach(Picture picture) => pictures.Add(picture);

    public void Detach(Picture picture) => pictures.Remove(picture);

    public Prompt? Compose(string draft)
    {
        var text = Pastes().Replace(draft, found => pasted.TryGetValue(int.Parse(found.Groups[1].Value), out var full) ? full : found.Value).Trim();
        if (text.Length == 0 && pictures.Count == 0)
        {
            return null;
        }

        return new Prompt(text.Length > 0 ? text : Unworded(pictures.Count), [.. pictures]);
    }

    public void Clear()
    {
        pasted.Clear();
        pictures.Clear();
    }

    private static string Unworded(int count) => count == 1 ? "(1 picture attached)" : $"({count} pictures attached)";

    [GeneratedRegex(@"\[Pasted text #(\d+)(?: \+\d+ lines)?\]")]
    private static partial Regex Pastes();
}
