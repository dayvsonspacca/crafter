namespace Crafter.Presentation.Folders;

public sealed class HeadingEntryViewModel(string text) : PickerEntryViewModel
{
    public string Text { get; } = text;
}
