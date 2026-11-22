using Orbita.Persistence;
using Orbita.Preferences.Application;
using Orbita.Preferences.Domain;

namespace Orbita.Preferences.Infrastructure;

public sealed class JsonPreferencesRepository(string path) : IPreferencesRepository
{
    private readonly JsonFileStore<UserPreferences> _store = new(path, () => new UserPreferences());

    public async Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        var preferences = await _store.ReadAsync(cancellationToken);
        preferences.Validate();
        return preferences;
    }

    public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default) =>
        _store.UpdateAsync(_ => preferences, cancellationToken);
}
