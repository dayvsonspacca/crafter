using CommunityToolkit.Mvvm.ComponentModel;
using Crafter.Core.Agents;

namespace Crafter.Desktop.Agents;

public sealed partial class EntrySetting : ObservableObject
{
    private readonly Entry entry;

    private readonly Action<string, string?> change;

    private readonly bool ready;

    public EntrySetting(Entry entry, Configuration configuration, Action<string, string?> change)
    {
        this.entry = entry;
        this.change = change;
        Text = entry.Value(configuration);
        ready = true;
    }

    public string Title => entry.Title;

    public string About => entry.About;

    public string Fallback => entry.Fallback;

    [ObservableProperty]
    public partial string Text { get; set; }

    partial void OnTextChanged(string value)
    {
        if (ready)
        {
            change(entry.Key, value.Trim() == entry.Fallback ? null : value.Trim());
        }
    }
}
