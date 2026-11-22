using Orbita.Localization;
using Orbita.SharedKernel;

namespace Orbita.Desktop.Presentation;

public abstract class LocalizedViewModel : ObservableObject, IDisposable
{
    protected LocalizedViewModel(ILocalizer localizer)
    {
        L = localizer;
        L.LanguageChanged += OnLanguageChanged;
    }

    public ILocalizer L { get; }
    private void OnLanguageChanged(object? sender, EventArgs arguments) => RaisePropertyChanged(string.Empty);
    public void Dispose() { L.LanguageChanged -= OnLanguageChanged; GC.SuppressFinalize(this); }
}
