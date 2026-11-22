using Orbita.SharedKernel;

namespace Orbita.Desktop.Services;

public sealed class Diagnostics(AppPaths paths)
{
    public void Log(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(paths.DataDirectory);
            File.AppendAllText(paths.Log, $"{DateTimeOffset.UtcNow:O}\n{exception}\n\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static string ResourceKey(Exception exception) => exception switch
    {
        DomainException domain => domain.ResourceKey,
        UnauthorizedAccessException => "ErrorPermission",
        FileNotFoundException or DirectoryNotFoundException => "ErrorFileMissing",
        IOException => "ErrorIo",
        _ => "ErrorUnexpected"
    };
}
