namespace Crafter.Core.Conversations.Replies;

// A tool by what it does and by the name its agent gives it; a served tool is named by its server.
public sealed record Tool(ToolKind Kind, string Name);
