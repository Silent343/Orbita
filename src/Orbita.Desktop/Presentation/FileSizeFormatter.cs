using System.Globalization;

namespace Orbita.Desktop.Presentation;

public static class FileSizeFormatter
{
    public static string Format(long bytes, CultureInfo culture)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value.ToString(index == 0 ? "N0" : "N1", culture)} {units[index]}";
    }
}
