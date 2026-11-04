using CommunityToolkit.Mvvm.Input;
using Crafter.Core.Conversations;

namespace Crafter.Presentation.Conversations.Composing;

public sealed partial class AttachmentViewModel(Picture picture, Action<AttachmentViewModel> remove)
{
    public Picture Picture { get; } = picture;

    [RelayCommand]
    private void Remove() => remove(this);
}
