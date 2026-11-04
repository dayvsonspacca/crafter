namespace Crafter.Core.Conversations.Replies;

// What a tool does, in Crafter's words; each agent sorts its own tools into these.
public enum ToolKind
{
    Other,
    Shell,
    Read,
    Edit,
    Write,
    Search,
    Web,
    Delegate,
    Served,
}
