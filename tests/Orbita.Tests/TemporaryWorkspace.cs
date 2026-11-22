namespace Orbita.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
    private readonly string _parent = Path.GetFullPath(Environment.GetEnvironmentVariable("ORBITA_TEST_ROOT")
        ?? Path.Combine(Path.GetTempPath(), "Orbita-tests"));

    public TemporaryWorkspace()
    {
        Root = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string FilePath(string name) => Path.Combine(Root, name);
    public void Dispose()
    {
        var resolved = Path.GetFullPath(Root);
        if (resolved.StartsWith(_parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}
