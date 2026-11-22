using Orbita.Desktop.Services;
using Orbita.Localization;
using Orbita.Preferences.Domain;

namespace Orbita.Desktop.Presentation;

public sealed class SettingsViewModel(ILocalizer localizer, PreferencesState state) : LocalizedViewModel(localizer)
{
    public UserPreferences Current => state.Current;
    public string OutputDescription => Current.OutputDirectory ?? L["OutputDefault"];

    public async Task SaveAsync(Func<UserPreferences, UserPreferences> update)
    {
        await state.UpdateAsync(update);
        RaisePropertyChanged(string.Empty);
    }
}
