using System.Text.RegularExpressions;
using Crafter.Core.Conversations;

namespace Crafter.Presentation.Conversations.Composing;

public sealed partial class Attachments
{
    public const int LongestPaste = 800;

    public const int MostPastedLines = 8;

    private readonly Dictionary<int, string> pasted = [];

    private readonly List<Picture> pictures = [];

    // Files go to the agent as @ references after the words, the way a typed mention does.
    private readonly List<string> files = [];

    private int last;

    public IReadOnlyList<Picture> Pictures => pictures;

    public IReadOnlyList<string> Files => files;

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

    public void Attach(string file) => files.Add(file);

    public void Detach(string file) => files.Remove(file);

    public Prompt? Compose(string draft)
    {
        var text = Pastes().Replace(draft, found => pasted.TryGetValue(int.Parse(found.Groups[1].Value), out var full) ? full : found.Value).Trim();
        if (text.Length == 0 && pictures.Count == 0 && files.Count == 0)
        {
            return null;
        }

        var references = string.Join(' ', files);
        var words = (text.Length > 0, files.Count > 0) switch
        {
            (true, true) => $"{text}\n\n{references}",
            (true, false) => text,
            (false, true) => references,
            _ => Unworded(pictures.Count),
        };

        return new Prompt(words, [.. pictures]);
    }

    public void Clear()
    {
        pasted.Clear();
        pictures.Clear();
        files.Clear();
    }

    private static string Unworded(int count) => count == 1 ? "(1 picture attached)" : $"({count} pictures attached)";

    [GeneratedRegex(@"\[Pasted text #(\d+)(?: \+\d+ lines)?\]")]
    private static partial Regex Pastes();
}
