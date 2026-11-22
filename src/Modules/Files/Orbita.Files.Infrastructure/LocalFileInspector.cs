using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

public sealed class LocalFileInspector : IFileInspector
{
    public Task<FileAsset> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var information = new FileInfo(path);
        if (!information.Exists) throw new DomainException("ErrorFileMissing");
        return Task.FromResult(FileAsset.Create(information.FullName, information.Length, information.LastWriteTimeUtc));
    }
}
