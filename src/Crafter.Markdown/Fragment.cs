namespace Crafter.Markdown;

// A stretch of text with one look; a hard line break is a "\n" inside the text.
public sealed record Fragment(string Text, Look Look = Look.Plain, string? Link = null);

[Flags]
public enum Look
{
    Plain = 0,
    Bold = 1,
    Italic = 2,
    Strike = 4,
    Code = 8,
}
