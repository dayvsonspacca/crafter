namespace Crafter.Presentation.Folders;

public sealed class HeadingEntry(string text) : PickerEntry
{
    public string Text { get; } = text;
}
