namespace Crafter.Core.Library;

public abstract record SyncOutcome;

public sealed record Synced(string Commit) : SyncOutcome;

public sealed record SyncFailed(string Reason) : SyncOutcome;
