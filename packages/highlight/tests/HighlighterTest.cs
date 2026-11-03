namespace Crafter.Highlight.Tests;

public sealed class HighlighterTest
{
    [Fact]
    public void ShadesTheCSharpOfARecord()
    {
        Assert.Equal(
            [
                ("public", Shade.Keyword), ("sealed", Shade.Keyword), ("record", Shade.Keyword), ("ChatSummary", Shade.Type),
                ("string", Shade.Keyword), ("DateTimeOffset", Shade.Type), ("int", Shade.Keyword),
            ],
            Shaded("public sealed record ChatSummary(string Title, DateTimeOffset LastOpened, int Turns);", Grammars.CSharp));
    }

    [Fact]
    public void KeepsTheSourceIntact()
    {
        const string code = "var x = 1; // one\n/* two */ \"three\"";

        Assert.Equal(code, string.Concat(Highlighter.Tokens(code, Grammars.CSharp).Select(token => token.Text)));
    }

    [Fact]
    public void ShadesCommentsToTheEndOfTheLineOrTheirClose()
    {
        Assert.Equal(
            [("// first", Shade.Comment), ("/* second\nstill */", Shade.Comment), ("int", Shade.Keyword)],
            Shaded("// first\n/* second\nstill */ int", Grammars.CSharp));
    }

    [Fact]
    public void RunsAnUnclosedCommentToTheEnd()
    {
        Assert.Equal([("/* never closed", Shade.Comment)], Shaded("/* never closed", Grammars.CSharp));
        Assert.Equal([("# last line", Shade.Comment)], Shaded("# last line", Grammars.Python));
    }

    [Fact]
    public void ShadesStringsWithTheirEscapesAndPrefixes()
    {
        Assert.Equal(
            [("\"say \\\"hi\\\"\"", Shade.Text), ("$\"{index}. done\"", Shade.Text), ("@\"C:\\temp\\\"", Shade.Text)],
            Shaded("a = \"say \\\"hi\\\"\"; b = $\"{index}. done\"; c = @\"C:\\temp\\\";", Grammars.CSharp));
    }

    [Fact]
    public void ShadesTripleQuotedText()
    {
        Assert.Equal([("\"\"\"\nraw \"text\"\n\"\"\"", Shade.Text)], Shaded("\"\"\"\nraw \"text\"\n\"\"\"", Grammars.CSharp));
        Assert.Equal([("'''never closed", Shade.Text)], Shaded("'''never closed", Grammars.Python));
    }

    [Fact]
    public void StopsAQuoteAtTheEndOfTheLineUnlessItIsATemplate()
    {
        Assert.Equal([("\"open\n", Shade.Text), ("var", Shade.Keyword)], Shaded("\"open\nvar", Grammars.Script));
        Assert.Equal([("`multi\nline`", Shade.Text)], Shaded("`multi\nline`", Grammars.Script));
        Assert.Equal([("'never", Shade.Text)], Shaded("'never", Grammars.Script));
    }

    [Fact]
    public void ShadesNumbersButNotDigitsInsideNames()
    {
        Assert.Equal([("5", Shade.Number), ("1.5", Shade.Number), ("0x1F", Shade.Number), ("10", Shade.Number)], Shaded("x1 = 5 + 1.5 * 0x1F - 10.", Grammars.Plain));
    }

    [Fact]
    public void ShadesBracketAttributesAtTheStartOfALine()
    {
        Assert.Equal(
            [("Obsolete", Shade.Attribute), ("\"Use LatestAsync\"", Shade.Text), ("Item", Shade.Type)],
            Shaded("    [Obsolete(\"Use LatestAsync\")]\nlist[Item]", Grammars.CSharp));
    }

    [Fact]
    public void ShadesAnnotationsAndDecorators()
    {
        Assert.Equal([("@Override", Shade.Attribute), ("public", Shade.Keyword)], Shaded("@Override public", Grammars.Java));
        Assert.Equal([("@cache", Shade.Attribute), ("def", Shade.Keyword)], Shaded("@cache\ndef run(): pass", Grammars.Python)[..2]);
        Assert.Empty(Shaded("@ alone", Grammars.Java));
    }

    [Fact]
    public void ReadsSqlKeywordsInAnyCase()
    {
        Assert.Equal(
            [("SELECT", Shade.Keyword), ("from", Shade.Keyword), ("-- all chats", Shade.Comment)],
            Shaded("SELECT title from Chats -- all chats", Grammars.Sql));
    }

    [Fact]
    public void LeavesTypesAloneWhereTheyMeanNothing()
    {
        Assert.Equal([("export", Shade.Keyword)], Shaded("export Path=/usr", Grammars.Shell));
        Assert.Equal(
            [("\"name\"", Shade.Text), ("true", Shade.Keyword), ("\"n\"", Shade.Text), ("3", Shade.Number)],
            Shaded("{\"name\": true, \"n\": 3}", Grammars.Json));
    }

    [Fact]
    public void TellsTypesFromTheNamesTheyDeclare()
    {
        Assert.Equal(
            [
                ("public", Shade.Keyword), ("static", Shade.Keyword), ("Task", Shade.Type), ("string", Shade.Keyword),
                ("string", Shade.Keyword), ("CancellationToken", Shade.Type), ("default", Shade.Keyword),
            ],
            Shaded("public static Task<string> LatestAsync(string File, CancellationToken Token = default)", Grammars.CSharp));
        Assert.Equal([("File", Shade.Type)], Shaded("File.OpenRead(Path)", Grammars.CSharp)[..1]);
        Assert.Equal([("Item", Shade.Type)], Shaded("Item[] Items", Grammars.CSharp));
    }

    [Fact]
    public void SkipsWordsThatAreOnlyCapitals()
    {
        Assert.Empty(Shaded("ID URL", Grammars.CSharp));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("CS")]
    [InlineData(" c# ")]
    public void FindsTheGrammarOfALanguage(string language)
    {
        Assert.Same(Grammars.CSharp, Grammars.For(language));
    }

    [Theory]
    [InlineData("java")]
    [InlineData("kotlin")]
    [InlineData("tsx")]
    [InlineData("php")]
    [InlineData("python")]
    [InlineData("rust")]
    [InlineData("go")]
    [InlineData("cpp")]
    [InlineData("bash")]
    [InlineData("sql")]
    [InlineData("json")]
    [InlineData("yaml")]
    public void KnowsTheCommonLanguages(string language)
    {
        Assert.NotSame(Grammars.Plain, Grammars.For(language));
    }

    [Fact]
    public void FallsBackToAPlainGrammar()
    {
        Assert.Same(Grammars.Plain, Grammars.For("brainfuck"));
        Assert.Equal([("true", Shade.Keyword)], Shaded("x: true", Grammars.Yaml));
    }

    private static List<(string, Shade)> Shaded(string code, Grammar grammar) =>
        [.. Highlighter.Tokens(code, grammar).Where(token => token.Shade != Shade.Plain).Select(token => (token.Text, token.Shade))];
}
