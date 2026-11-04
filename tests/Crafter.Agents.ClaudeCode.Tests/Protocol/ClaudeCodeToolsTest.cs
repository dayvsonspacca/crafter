using System.Text.Json;
using Crafter.Agents.ClaudeCode.Protocol;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Agents.ClaudeCode.Tests.Protocol;

public sealed class ClaudeCodeToolsTest
{
    [Theory]
    [InlineData("Bash", """{"command":"dotnet test\ndotnet build"}""", "dotnet test")]
    [InlineData("Read", """{"file_path":"/home/hero/crafter/apps/Program.cs"}""", "apps/Program.cs")]
    [InlineData("Read", """{"file_path":"/etc/hosts"}""", "/etc/hosts")]
    [InlineData("Edit", """{"file_path":"/home/hero/crafter/a.cs","old_string":"x","new_string":"y\nz\n"}""", "a.cs", 2, 1)]
    [InlineData("MultiEdit", """{"file_path":"/home/hero/crafter/a.cs","edits":[{"old_string":"a","new_string":"b\nc"},{"old_string":"d\ne","new_string":""},7]}""", "a.cs", 2, 3)]
    [InlineData("MultiEdit", """{"file_path":"/home/hero/crafter/a.cs"}""", "a.cs", 0, 0)]
    [InlineData("Write", """{"file_path":"/home/hero/crafter/b.md","content":"one\ntwo\nthree"}""", "b.md", 3)]
    [InlineData("NotebookEdit", """{"notebook_path":"/home/hero/crafter/n.ipynb"}""", "n.ipynb")]
    [InlineData("Grep", """{"pattern":"TODO"}""", "TODO")]
    [InlineData("Glob", """{}""", "")]
    [InlineData("WebFetch", """{"url":"https://example.com"}""", "https://example.com")]
    [InlineData("WebSearch", """{"query":"avalonia"}""", "avalonia")]
    [InlineData("Task", """{"description":"Look around"}""", "Look around")]
    [InlineData("Agent", """{}""", "")]
    [InlineData("TodoWrite", """{"todos":[]}""", "TodoWrite")]
    [InlineData("Custom", """{"command":"go"}""", "go")]
    [InlineData("Custom", """[]""", "Custom")]
    public void DescribesWhatEachToolIsAbout(string tool, string input, string summary, int? added = null, int? removed = null)
    {
        using var document = JsonDocument.Parse(input);

        Assert.Equal((summary, added is { } lines ? new Changes(lines, removed) : null), ClaudeCodeTools.Describe(tool, document.RootElement, "/home/hero/crafter"));
    }

    [Theory]
    [InlineData("mcp__crafter__list_projects", """{}""", ToolKind.Served, "crafter", "list projects")]
    [InlineData("mcp__crafter__set_project_icon", """{"folder":"/home/hero/crafter/hyprland","image":"/tmp/icon.png"}""", ToolKind.Served, "crafter", "set project icon · hyprland")]
    [InlineData("mcp__claude_ai_Claude_Docs__batch", """{"opId":7,"container":{"kind":"project"}}""", ToolKind.Served, "Claude Docs", "batch")]
    [InlineData("mcp__plugin-miro__get-board", """{"board":"b1\nmore"}""", ToolKind.Served, "plugin-miro", "get board · b1")]
    [InlineData("Bash", """{"command":"ls"}""", ToolKind.Shell, "Bash", "ls")]
    [InlineData("mcp__broken", """{}""", ToolKind.Other, "mcp__broken", "mcp__broken")]
    public void NamesAToolServersToolByItsServerAndWhatItDid(string tool, string input, ToolKind kind, string shown, string summary)
    {
        using var document = JsonDocument.Parse(input);

        var named = ClaudeCodeTools.Shown(tool, document.RootElement, "/home/hero/crafter");

        Assert.Equal((new Tool(kind, shown), summary), (named.Tool, named.Summary));
    }

    [Theory]
    [InlineData("Bash", ToolKind.Shell)]
    [InlineData("Read", ToolKind.Read)]
    [InlineData("MultiEdit", ToolKind.Edit)]
    [InlineData("NotebookEdit", ToolKind.Edit)]
    [InlineData("Write", ToolKind.Write)]
    [InlineData("Glob", ToolKind.Search)]
    [InlineData("WebSearch", ToolKind.Web)]
    [InlineData("Task", ToolKind.Delegate)]
    [InlineData("TodoWrite", ToolKind.Other)]
    public void SortsItsToolsIntoCraftersKinds(string tool, ToolKind kind) =>
        Assert.Equal(kind, ClaudeCodeTools.Kind(tool));

    [Fact]
    public void KeepsFullPathsWithoutAFolder()
    {
        using var document = JsonDocument.Parse("""{"file_path":"/home/hero/crafter/a.cs"}""");

        Assert.Equal("/home/hero/crafter/a.cs", ClaudeCodeTools.Describe("Read", document.RootElement, null).Summary);
    }
}
