using System.Globalization;

namespace Crafter.Desktop.Conversations;

public static class Elapsed
{
    public static string Since(DateTimeOffset then, DateTimeOffset now)
    {
        var gone = now - then;

        return gone switch
        {
            { TotalMinutes: < 1 } => "now",
            { TotalHours: < 1 } => $"{(int)gone.TotalMinutes}m ago",
            { TotalDays: < 1 } => $"{(int)gone.TotalHours}h ago",
            { TotalDays: < 2 } => "yesterday",
            { TotalDays: < 7 } => $"{(int)gone.TotalDays} days ago",
            _ => then.ToLocalTime().ToString("d MMM", CultureInfo.InvariantCulture),
        };
    }
}
