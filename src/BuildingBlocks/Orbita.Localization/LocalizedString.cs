using Orbita.SharedKernel;

namespace Orbita.Localization;

/// <summary>A stable observable value that WinUI can bind without indexer change notifications.</summary>
public sealed class LocalizedString(string value) : ObservableObject
{
    private string _value = value;
    public string Value { get => _value; internal set => SetProperty(ref _value, value); }
}
