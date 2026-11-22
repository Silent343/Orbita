using Orbita.Activity.Application;
using Orbita.Activity.Domain;
using Orbita.Localization;

namespace Orbita.Desktop.Presentation;

public sealed class HistoryViewModel(ILocalizer localizer, ActivityService activity) : LocalizedViewModel(localizer)
{
    public IReadOnlyList<OperationRecord> Operations { get; private set; } = [];
    public async Task LoadAsync()
    {
        Operations = await activity.GetRecentAsync();
        RaisePropertyChanged(nameof(Operations));
    }
}
