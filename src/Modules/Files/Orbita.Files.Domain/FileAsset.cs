using Orbita.SharedKernel;

namespace Orbita.Files.Domain;

public enum FileCategory { Pdf, Image, Text, Other, Word, Video, Ebook, Spreadsheet, Table }
public enum ActionGroup { Convert, Tools }

public sealed record FileAsset
{
    private static readonly string[] WindowsBitmapExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];
    private static readonly string[] RawExtensions = [".dng", ".cr2", ".cr3", ".nef", ".arw", ".raf", ".orf", ".rw2"];
    public static IReadOnlyList<string> PhotoExtensions { get; } =
        Array.AsReadOnly(WindowsBitmapExtensions.Concat([".heic", ".heif", ".webp", ".avif"]).Concat(RawExtensions).ToArray());

    private FileAsset(string path, long size, DateTimeOffset modified)
    {
        Path = path;
        Size = size;
        Modified = modified;
        Extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        Category = Extension switch
        {
            ".pdf" => FileCategory.Pdf,
            _ when PhotoExtensions.Contains(Extension) || Extension is ".svg" or ".gif" or ".ico" => FileCategory.Image,
            ".txt" or ".md" or ".markdown" or ".html" or ".htm" => FileCategory.Text,
            ".docx" => FileCategory.Word,
            ".epub" => FileCategory.Ebook,
            ".xls" or ".xlsx" or ".ods" => FileCategory.Spreadsheet,
            ".csv" or ".json" or ".parquet" => FileCategory.Table,
            ".mp4" or ".m4v" or ".mkv" or ".mov" or ".avi" or ".webm" or ".wmv" or ".mpeg" or ".mpg" or ".3gp" => FileCategory.Video,
            _ => FileCategory.Other
        };
    }

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public long Size { get; }
    public string Extension { get; }
    public bool IsRawPhoto => RawExtensions.Contains(Extension);
    public bool IsPhoto => PhotoExtensions.Contains(Extension);
    public bool UsesWindowsBitmap => WindowsBitmapExtensions.Contains(Extension);
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
