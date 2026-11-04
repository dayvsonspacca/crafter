using Crafter.Presentation.Library;

namespace Crafter.Presentation.Preferences;

public sealed class LibraryGroupViewModel(LibraryViewModel library) : PreferenceGroupViewModel("Library")
{
    public LibraryViewModel Library { get; } = library;
}
