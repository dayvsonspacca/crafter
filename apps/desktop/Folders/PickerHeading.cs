namespace Crafter.Desktop.Folders;

public sealed class PickerHeading(string text) : PickerEntry
{
    public string Text { get; } = text;
}
