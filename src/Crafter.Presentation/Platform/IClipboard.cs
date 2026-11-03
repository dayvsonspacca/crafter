namespace Crafter.Presentation.Platform;

public interface IClipboard
{
    Task Copy(string text);
}
