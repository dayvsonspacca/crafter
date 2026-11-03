namespace Crafter.Desktop.Platform;

public interface IClipboard
{
    Task Copy(string text);
}
