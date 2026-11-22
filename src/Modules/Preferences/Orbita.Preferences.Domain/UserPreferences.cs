using Orbita.SharedKernel;

namespace Orbita.Preferences.Domain;

public sealed record UserPreferences
{
    public string Language { get; init; } = "es";
    public string Theme { get; init; } = "dark";
    public bool ExplorerShortcutEnabled { get; init; } = true;
    public int ShiftHoldMilliseconds { get; init; } = 350;
    public string? OutputDirectory { get; init; }

    public void Validate()
    {
        if (Language is not ("es" or "en")) throw new DomainException("ErrorInvalidLanguage");
        if (Theme is not ("dark" or "light" or "system")) throw new DomainException("ErrorInvalidTheme");
        if (ShiftHoldMilliseconds is < 200 or > 1500) throw new DomainException("ErrorInvalidDelay");
        if (OutputDirectory is not null && !Path.IsPathFullyQualified(OutputDirectory))
            throw new DomainException("ErrorInvalidPath");
    }
}
