namespace Crafter.Core.Agents;

public sealed record AgentId
{
    public AgentId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
