namespace Crafter.Highlight;

public static class Grammars
{
    private static readonly (string, string)[] SlashStar = [("/*", "*/")];

    private static readonly string[] Slashes = ["//"];

    private static readonly string[] Hash = ["#"];

    public static readonly Grammar CSharp = new()
    {
        Keywords = Words("abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach get goto if implicit in init int interface internal is lock long namespace new null object operator out override params partial private protected public readonly record ref required return sbyte sealed set short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile when where while with yield"),
        LineComments = Slashes,
        BlockComments = SlashStar,
        StringPrefixes = "$@",
        BracketAttributes = true,
    };

    public static readonly Grammar Java = new()
    {
        Keywords = Words("abstract assert boolean break byte case catch char class const continue default do double else enum extends false final finally float for if implements import instanceof int interface long native new null package private protected public record return sealed short static super switch synchronized this throw throws true try var void volatile while yield"),
        LineComments = Slashes,
        BlockComments = SlashStar,
        AtAttributes = true,
    };

    public static readonly Grammar Kotlin = new()
    {
        Keywords = Words("as break class companion const continue data do else enum false for fun if import in interface internal is null object open override package private protected public return sealed super this throw true try typealias val var when while"),
        LineComments = Slashes,
        BlockComments = SlashStar,
        AtAttributes = true,
    };

    public static readonly Grammar Script = new()
    {
        Keywords = Words("as async await break case catch class const continue debugger default delete do else enum export extends false finally for from function if implements import in instanceof interface let new null of private protected public readonly return static super switch this throw true try type typeof undefined var void while with yield"),
        LineComments = Slashes,
        BlockComments = SlashStar,
        Quotes = "\"'`",
        AtAttributes = true,
    };

    public static readonly Grammar Php = new()
    {
        Keywords = Words("abstract and array as break case catch class clone const continue declare default do echo else elseif enum extends false final finally fn for foreach function global if implements include instanceof interface match namespace new null or private protected public readonly require return self static switch throw trait true try use var while yield"),
        LineComments = ["//", "#"],
        BlockComments = SlashStar,
    };

    public static readonly Grammar Python = new()
    {
        Keywords = Words("and as assert async await break class continue def del elif else except False finally for from global if import in is lambda None nonlocal not or pass raise return True try while with yield"),
        LineComments = Hash,
        AtAttributes = true,
    };

    public static readonly Grammar Rust = new()
    {
        Keywords = Words("as async await break const continue crate dyn else enum extern false fn for if impl in let loop match mod move mut pub ref return self Self static struct super trait true type unsafe use where while"),
        LineComments = Slashes,
        BlockComments = SlashStar,
        Quotes = "\"",
    };

    public static readonly Grammar Go = new()
    {
        Keywords = Words("break case chan const continue default defer else fallthrough false for func go goto if import interface map nil package range return select struct switch true type var"),
        LineComments = Slashes,
        BlockComments = SlashStar,
        Quotes = "\"'`",
    };

    public static readonly Grammar C = new()
    {
        Keywords = Words("auto bool break case catch char class const constexpr continue default delete do double else enum explicit extern false float for friend goto if inline int long namespace new nullptr operator private protected public register return short signed sizeof static struct switch template this throw true try typedef typename union unsigned using virtual void volatile while"),
        LineComments = Slashes,
        BlockComments = SlashStar,
    };

    public static readonly Grammar Shell = new()
    {
        Keywords = Words("case do done elif else esac export fi for function if in local read return then until while"),
        LineComments = Hash,
        KnowsTypes = false,
    };

    public static readonly Grammar Sql = new()
    {
        Keywords = Words("add all alter and as asc between by case create delete desc distinct drop else end exists false from group having in index insert inner into is join key left like limit not null on or order outer primary references right select set table then true union unique update values when where with"),
        LineComments = ["--"],
        BlockComments = SlashStar,
        KnowsTypes = false,
        IgnoresCase = true,
    };

    public static readonly Grammar Json = new()
    {
        Keywords = Words("true false null"),
        Quotes = "\"",
        KnowsTypes = false,
    };

    public static readonly Grammar Yaml = new()
    {
        Keywords = Words("true false null yes no on off"),
        LineComments = Hash,
        KnowsTypes = false,
    };

    public static readonly Grammar Plain = new()
    {
        Keywords = Words("if else for while return function class def fn var let const true false null new import"),
        LineComments = ["//", "#"],
        BlockComments = SlashStar,
        Quotes = "\"'`",
    };

    private static readonly Dictionary<string, Grammar> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = CSharp,
        ["csharp"] = CSharp,
        ["c#"] = CSharp,
        ["java"] = Java,
        ["kotlin"] = Kotlin,
        ["kt"] = Kotlin,
        ["js"] = Script,
        ["javascript"] = Script,
        ["jsx"] = Script,
        ["ts"] = Script,
        ["typescript"] = Script,
        ["tsx"] = Script,
        ["php"] = Php,
        ["py"] = Python,
        ["python"] = Python,
        ["rs"] = Rust,
        ["rust"] = Rust,
        ["go"] = Go,
        ["golang"] = Go,
        ["c"] = C,
        ["cpp"] = C,
        ["c++"] = C,
        ["h"] = C,
        ["hpp"] = C,
        ["sh"] = Shell,
        ["bash"] = Shell,
        ["shell"] = Shell,
        ["zsh"] = Shell,
        ["sql"] = Sql,
        ["json"] = Json,
        ["jsonc"] = Json,
        ["yaml"] = Yaml,
        ["yml"] = Yaml,
    };

    public static Grammar For(string language) => Named.GetValueOrDefault(language.Trim()) ?? Plain;

    private static HashSet<string> Words(string words) => [.. words.Split(' ')];
}
