namespace Crafter.Core.Conversations;

public sealed class Picture : IEquatable<Picture>
{
    public Picture(string mediaType, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentOutOfRangeException.ThrowIfZero(data.Length);
        MediaType = mediaType;
        Data = data;
    }

    public string MediaType { get; }

    public byte[] Data { get; }

    public bool Equals(Picture? other) =>
        other is not null && MediaType == other.MediaType && Data.AsSpan().SequenceEqual(other.Data);

    public override bool Equals(object? obj) => Equals(obj as Picture);

    public override int GetHashCode() => HashCode.Combine(MediaType, Data.Length);
}
