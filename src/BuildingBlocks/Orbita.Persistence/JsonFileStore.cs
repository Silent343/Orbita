using System.Text.Json;

namespace Orbita.Persistence;

public sealed class JsonFileStore<T>(string path, Func<T> createDefault)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadCoreAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(Func<T, T> update, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            var next = update(await ReadCoreAsync(cancellationToken));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, next, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath)) File.Delete(temporaryPath);
            _gate.Release();
        }
    }

    private async Task<T> ReadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return createDefault();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken)
            ?? throw new JsonException("The stored document is empty.");
    }
}
