namespace Crafter.Core.Conversations;

public sealed record Acted(string Id, bool Failed, string Output) : Reply;
