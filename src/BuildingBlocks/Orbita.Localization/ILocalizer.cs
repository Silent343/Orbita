using System.ComponentModel;
using System.Globalization;

namespace Orbita.Localization;

public interface ILocalizer : INotifyPropertyChanged
{
    string this[string key] { get; }
    IReadOnlyDictionary<string, LocalizedString> Strings { get; }
    CultureInfo Culture { get; }
    IReadOnlyList<string> SupportedLanguages { get; }
    event EventHandler? LanguageChanged;
    void ChangeLanguage(string language);
    string Format(string key, params object[] arguments);
}
