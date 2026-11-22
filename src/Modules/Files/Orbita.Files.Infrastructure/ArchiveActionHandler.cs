using System.IO.Compression;
using Orbita.Files.Application;
using Orbita.Files.Domain;

namespace Orbita.Files.Infrastructure;

public sealed class ArchiveActionHandler : IFileActionHandler
{
    public bool CanHandle(string actionId) => actionId == "zip";

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        using var output = OutputFile.Create(request.OutputDirectory,
            Path.GetFileNameWithoutExtension(request.File.Name), ".zip");
        using (var archive = new ZipArchive(output.Stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(request.File.Name, CompressionLevel.Optimal);
            await using var destination = entry.Open();
            await using var source = File.OpenRead(request.File.Path);
            await source.CopyToAsync(destination, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var length = output.Stream.Length;
        output.Commit();
        return new ActionResult([output.Path], length);
    }
}
