namespace Crafter.Core.Conversations;

public sealed record Question(string Text, string Header, bool Several, IReadOnlyList<Pick> Picks);
