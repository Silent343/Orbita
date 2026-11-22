namespace Orbita.Desktop.Services;

public sealed class AppPaths
{
    public string DataDirectory { get; } = Path.GetFullPath(
        Environment.GetEnvironmentVariable("ORBITA_DATA_DIRECTORY") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbita"));
    public string Preferences => Path.Combine(DataDirectory, "preferences.json");
    public string Activity => Path.Combine(DataDirectory, "activity.json");
    public string Log => Path.Combine(DataDirectory, "diagnostics.log");
}
