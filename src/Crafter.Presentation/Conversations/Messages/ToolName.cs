using Crafter.Core.Conversations.Replies;

namespace Crafter.Presentation.Conversations.Messages;

// The name the chat gives a tool: Crafter's word for what it does, or its own name when Crafter has none.
public static class ToolName
{
    public static string Of(Tool tool) => tool.Kind switch
    {
        ToolKind.Shell => "Shell",
        ToolKind.Read => "Read",
        ToolKind.Edit => "Edit",
        ToolKind.Write => "Write",
        ToolKind.Search => "Search",
        ToolKind.Web => "Web",
        ToolKind.Delegate => "Agent",
        _ => tool.Name,
    };
}
