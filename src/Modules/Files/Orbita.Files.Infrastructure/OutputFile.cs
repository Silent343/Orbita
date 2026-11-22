namespace Orbita.Files.Infrastructure;

/// <summary>Reserves a new name atomically and removes incomplete output on failure.</summary>
public sealed class OutputFile : IDisposable
{
    private bool _committed;
    private OutputFile(string path, FileStream stream) { Path = path; Stream = stream; }
    public string Path { get; }
    public FileStream Stream { get; }

    public static OutputFile Create(string directory, string stem, string extension)
    {
        Directory.CreateDirectory(directory);
        for (var suffix = 0; suffix < 10000; suffix++)
        {
            var name = suffix == 0 ? stem + extension : $"{stem} ({suffix}){extension}";
            var path = System.IO.Path.Combine(directory, name);
            try { return new OutputFile(path, new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)); }
            catch (IOException) when (File.Exists(path)) { }
        }
        throw new IOException("Output names exhausted.");
    }

    public void Commit() => _committed = true;

    public void Dispose()
    {
        Stream.Dispose();
        if (!_committed && File.Exists(Path)) File.Delete(Path);
    }
}
