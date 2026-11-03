using System.Text;

namespace Crafter.Highlight;

public static class Highlighter
{
    private static readonly HashSet<string> Primitives =
    [
        "bool", "boolean", "byte", "char", "decimal", "double", "dynamic", "float", "int", "long", "object", "sbyte", "short",
        "string", "uint", "ulong", "ushort", "var", "void", "val", "let", "const", "auto",
    ];

    public static IReadOnlyList<Token> Tokens(string code, Grammar grammar)
    {
        var tokens = new List<Token>();
        var plain = new StringBuilder();
        var at = 0;
        var lineStart = true;

        while (at < code.Length)
        {
            if (Read(code, at, grammar, lineStart) is { } token)
            {
                if (token.Shade == Shade.Plain)
                {
                    plain.Append(token.Text);
                }
                else
                {
                    Flush(tokens, plain);
                    tokens.Add(token);
                }

                at += token.Text.Length;
                lineStart = false;

                continue;
            }

            var letter = code[at];
            plain.Append(letter);
            lineStart = letter == '\n' || lineStart && (char.IsWhiteSpace(letter) || letter == '[');
            at++;
        }

        Flush(tokens, plain);

        return tokens;
    }

    private static Token? Read(string code, int at, Grammar grammar, bool lineStart) =>
        Comment(code, at, grammar)
            ?? Quoted(code, at, grammar)
            ?? Number(code, at)
            ?? Attribute(code, at, grammar, lineStart)
            ?? Word(code, at, grammar);

    private static Token? Comment(string code, int at, Grammar grammar)
    {
        foreach (var (open, close) in grammar.BlockComments)
        {
            if (string.CompareOrdinal(code, at, open, 0, open.Length) == 0)
            {
                var end = code.IndexOf(close, at + open.Length, StringComparison.Ordinal);

                return new Token(end < 0 ? code[at..] : code[at..(end + close.Length)], Shade.Comment);
            }
        }

        foreach (var marker in grammar.LineComments)
        {
            if (string.CompareOrdinal(code, at, marker, 0, marker.Length) == 0)
            {
                var end = code.IndexOf('\n', at);

                return new Token(end < 0 ? code[at..] : code[at..end], Shade.Comment);
            }
        }

        return null;
    }

    private static Token? Quoted(string code, int at, Grammar grammar)
    {
        var start = at;
        var verbatim = false;

        while (at < code.Length && grammar.StringPrefixes.Contains(code[at]))
        {
            verbatim |= code[at] == '@';
            at++;
        }

        if (at >= code.Length || !grammar.Quotes.Contains(code[at]))
        {
            return null;
        }

        var quote = code[at];
        var fence = new string(quote, 3);

        if (string.CompareOrdinal(code, at, fence, 0, 3) == 0)
        {
            var close = code.IndexOf(fence, at + 3, StringComparison.Ordinal);

            return new Token(close < 0 ? code[start..] : code[start..(close + 3)], Shade.Text);
        }

        for (var end = at + 1; end < code.Length; end++)
        {
            if (code[end] == '\\' && !verbatim)
            {
                end++;
            }
            else if (code[end] == quote || code[end] == '\n' && quote != '`')
            {
                return new Token(code[start..(end + 1)], Shade.Text);
            }
        }

        return new Token(code[start..], Shade.Text);
    }

    private static Token? Number(string code, int at)
    {
        if (!char.IsAsciiDigit(code[at]) || at > 0 && (char.IsLetterOrDigit(code[at - 1]) || code[at - 1] == '_'))
        {
            return null;
        }

        var end = at;
        while (end < code.Length && (char.IsAsciiLetterOrDigit(code[end]) || code[end] is '_' or '.' && end + 1 < code.Length && char.IsAsciiDigit(code[end + 1])))
        {
            end++;
        }

        return new Token(code[at..end], Shade.Number);
    }

    private static Token? Attribute(string code, int at, Grammar grammar, bool lineStart)
    {
        if (grammar.AtAttributes && code[at] == '@' && Name(code, at + 1) is { Length: > 0 } annotated)
        {
            return new Token("@" + annotated, Shade.Attribute);
        }

        if (grammar.BracketAttributes && lineStart && at > 0 && code[at - 1] == '[' && Name(code, at) is { Length: > 0 } bracketed)
        {
            return new Token(bracketed, Shade.Attribute);
        }

        return null;
    }

    private static Token? Word(string code, int at, Grammar grammar)
    {
        if (Name(code, at) is not { Length: > 0 } word || at > 0 && (char.IsLetterOrDigit(code[at - 1]) || code[at - 1] == '_'))
        {
            return null;
        }

        if (grammar.IsKeyword(word))
        {
            return new Token(word, Shade.Keyword);
        }

        return grammar.KnowsTypes && char.IsUpper(word[0]) && word.Any(char.IsLower) && !Declared(code, at)
            ? new Token(word, Shade.Type)
            : new Token(word, Shade.Plain);
    }

    private static bool Declared(string code, int at)
    {
        var before = at - 1;
        while (before >= 0 && code[before] is ' ' or '\t')
        {
            before--;
        }

        if (before < 0)
        {
            return false;
        }

        if (code[before] == '.' || code[before] is '>' or ']' && before < at - 1)
        {
            return true;
        }

        var end = before + 1;
        while (before >= 0 && (char.IsLetterOrDigit(code[before]) || code[before] == '_'))
        {
            before--;
        }

        var previous = code[(before + 1)..end];

        return previous.Length > 0 && end < at && (Primitives.Contains(previous) || char.IsUpper(previous[0]) && previous.Any(char.IsLower));
    }

    private static string Name(string code, int at)
    {
        if (at >= code.Length || !(char.IsLetter(code[at]) || code[at] == '_'))
        {
            return string.Empty;
        }

        var end = at;
        while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] == '_'))
        {
            end++;
        }

        return code[at..end];
    }

    private static void Flush(List<Token> tokens, StringBuilder plain)
    {
        if (plain.Length > 0)
        {
            tokens.Add(new Token(plain.ToString(), Shade.Plain));
            plain.Clear();
        }
    }
}
