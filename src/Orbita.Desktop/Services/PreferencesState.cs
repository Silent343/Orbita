using Orbita.Localization;
using Orbita.Preferences.Application;
using Orbita.Preferences.Domain;

namespace Orbita.Desktop.Services;

public sealed class PreferencesState(PreferencesService service, ILocalizer localizer)
{
    private readonly SemaphoreSlim _updates = new(1, 1);
    public UserPreferences Current { get; private set; } = new();
    public event EventHandler? Changed;

    public async Task LoadAsync()
    {
        var preferences = await service.LoadAsync();
        Apply(preferences);
    }

    public async Task UpdateAsync(Func<UserPreferences, UserPreferences> update)
    {
        await _updates.WaitAsync();
        try
        {
            var preferences = update(Current);
            await service.SaveAsync(preferences);
            Apply(preferences);
        }
        finally { _updates.Release(); }
    }

    private void Apply(UserPreferences preferences)
    {
        Current = preferences;
        localizer.ChangeLanguage(preferences.Language);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
