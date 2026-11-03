using Crafter.Agents.ClaudeCode;
using Crafter.Core.Agents;

namespace Crafter.Desktop;

public static class Hiring
{
    public static Crew Crew() => new([new ClaudeCode()]);
}
