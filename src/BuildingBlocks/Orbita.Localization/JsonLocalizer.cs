using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Orbita.SharedKernel;

namespace Orbita.Localization;

public sealed class JsonLocalizer : ObservableObject, ILocalizer
{
    private readonly IReadOnlyDictionary<string, Dictionary<string, string>> _catalogs;
    private string _language = "es";

    public JsonLocalizer()
    {
        var assembly = typeof(JsonLocalizer).Assembly;
        _catalogs = new[] { "es", "en" }.ToDictionary(language => language, language =>
        {
            using var stream = assembly.GetManifestResourceStream($"Orbita.Localization.Resources.{language}.json")
                ?? throw new InvalidOperationException($"Missing localization catalog: {language}");
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        });
        Strings = _catalogs["en"].Keys.ToDictionary(key => key, key => new LocalizedString(this[key]));
    }

    public string this[string key] => _catalogs[_language].GetValueOrDefault(key)
        ?? _catalogs["en"].GetValueOrDefault(key) ?? $"[{key}]";
    public IReadOnlyDictionary<string, LocalizedString> Strings { get; }
    public CultureInfo Culture => CultureInfo.GetCultureInfo(_language);
    public IReadOnlyList<string> SupportedLanguages { get; } = ["es", "en"];
    public event EventHandler? LanguageChanged;

    public void ChangeLanguage(string language)
    {
        if (!_catalogs.ContainsKey(language)) throw new ArgumentOutOfRangeException(nameof(language));
        if (_language == language) return;
        _language = language;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
        foreach (var (key, text) in Strings) text.Value = this[key];
        RaisePropertyChanged(string.Empty);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Format(string key, params object[] arguments) => string.Format(Culture, this[key], arguments);
}
