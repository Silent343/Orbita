using Orbita.Preferences.Domain;

namespace Orbita.Preferences.Application;

public interface IPreferencesRepository
{
    Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}

public sealed class PreferencesService(IPreferencesRepository repository)
{
    public Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);

    public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        preferences.Validate();
        return repository.SaveAsync(preferences, cancellationToken);
    }
}
