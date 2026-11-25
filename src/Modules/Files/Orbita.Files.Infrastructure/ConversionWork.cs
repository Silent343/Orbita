using System.ComponentModel;
using System.Diagnostics;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

internal sealed class ConversionWork : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("Orbita-");
    public string DirectoryPath => _directory.FullName;
    public string PathFor(string name) => Path.Combine(DirectoryPath, name);

    public async Task<string> StageAsync(string source, string name, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        await using var input = File.OpenRead(source);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken);
        return path;
    }

    public static string FindTool(string variable, string executable, string missing, params string[] locations)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        var path = locations.Prepend(configured).FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)
            && Path.IsPathFullyQualified(candidate) && !candidate.StartsWith(@"\\", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(candidate), executable, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate));
        return path ?? throw new DomainException(missing);
    }

    public static string LocalTool(string folder, params string[] components) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "tools", folder }.Concat(components).ToArray());

    public static string WinGetLink(string name, Environment.SpecialFolder folder) =>
        Path.Combine(Environment.GetFolderPath(folder), folder == Environment.SpecialFolder.LocalApplicationData ? "Microsoft" : "",
            "WinGet", "Links", name);

    public async Task RunAsync(string tool, IEnumerable<string> arguments, string error,
        CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo(tool) { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = DirectoryPath };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var (key, value) in environment) start.Environment[key] = value;
        cancellationToken.ThrowIfCancellationRequested();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromMinutes(10));
        Process? started;
        try { started = Process.Start(start); }
        catch (Win32Exception) { throw new DomainException(error); }
        using var process = started ?? throw new DomainException(error);
        try { await process.WaitForExitAsync(limit.Token); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            cancellationToken.ThrowIfCancellationRequested();
            throw new DomainException("ErrorConversionTimeout");
        }
        if (process.ExitCode != 0) throw new DomainException(error);
    }

    public string RequireOutput(string path, string error)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new DomainException(error);
        return path;
    }

    public async Task<ActionResult> PublishAsync(ActionRequest request, IReadOnlyList<string> sourcePaths, CancellationToken token)
    {
        var outputs = new List<OutputFile>();
        var stem = Path.GetFileNameWithoutExtension(request.File.Name);
        try
        {
            foreach (var path in sourcePaths)
            {
                token.ThrowIfCancellationRequested();
                var stagedName = Path.GetFileNameWithoutExtension(path);
                var suffix = stagedName.StartsWith("result-", StringComparison.Ordinal) ? stagedName[6..] : "";
                var output = OutputFile.Create(request.OutputDirectory, stem + suffix, Path.GetExtension(path));
                outputs.Add(output);
                await using var source = File.OpenRead(path);
                await source.CopyToAsync(output.Stream, token);
            }
            token.ThrowIfCancellationRequested();
            var result = new ActionResult(outputs.Select(output => output.Path).ToArray(), outputs.Sum(output => output.Stream.Length));
            foreach (var output in outputs) output.Commit();
            return result;
        }
        finally { foreach (var output in outputs) output.Dispose(); }
    }

    public void Dispose()
    {
        try { _directory.Delete(recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
