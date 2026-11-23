using Orbita.SharedKernel;

namespace Orbita.Files.Domain;

public enum FileCategory { Pdf, Image, Text, Other, Word, Video }
public enum ActionGroup { Convert, Tools }

public sealed record FileAsset
{
    public static IReadOnlyList<string> ImageExtensions { get; } =
        Array.AsReadOnly(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".heic", ".heif" });

    private FileAsset(string path, long size, DateTimeOffset modified)
    {
        Path = path;
        Size = size;
        Modified = modified;
        Extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        Category = Extension switch
        {
            ".pdf" => FileCategory.Pdf,
            _ when ImageExtensions.Contains(Extension) => FileCategory.Image,
            ".txt" or ".md" => FileCategory.Text,
            ".docx" => FileCategory.Word,
            ".mp4" or ".m4v" or ".mkv" or ".mov" or ".avi" or ".webm" or ".wmv" or ".mpeg" or ".mpg" or ".3gp" => FileCategory.Video,
            _ => FileCategory.Other
        };
    }

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public long Size { get; }
    public string Extension { get; }
    public bool IsHeic => Extension is ".heic" or ".heif";
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
