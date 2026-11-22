using Orbita.SharedKernel;

namespace Orbita.Files.Domain;

public enum FileCategory { Pdf, Image, Text, Other }
public enum ActionGroup { Convert, Tools }

public sealed record FileAsset
{
    private FileAsset(string path, long size, DateTimeOffset modified)
    {
        Path = path;
        Size = size;
        Modified = modified;
        Extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        Category = Extension switch
        {
            ".pdf" => FileCategory.Pdf,
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" => FileCategory.Image,
            ".txt" or ".md" => FileCategory.Text,
            _ => FileCategory.Other
        };
    }

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public long Size { get; }
    public string Extension { get; }
    public FileCategory Category { get; }
    public DateTimeOffset Modified { get; }

    public static FileAsset Create(string path, long size, DateTimeOffset modified)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathFullyQualified(path))
            throw new DomainException("ErrorInvalidPath");
        if (size < 0) throw new DomainException("ErrorInvalidSize");
        return new FileAsset(System.IO.Path.GetFullPath(path), size, modified);
    }
}
