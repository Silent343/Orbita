using Orbita.Files.Application;
using Orbita.Files.Domain;
using Xunit;

namespace Orbita.Tests;

public sealed partial class VideoBooksAndExcelTests
{
    [Theory]
    [InlineData("clip.mov", "video.mp4")]
    [InlineData("clip.mp4", "video.webm")]
    [InlineData("clip.mkv", "video.gif")]
    [InlineData("clip.avi", "video.frames")]
    [InlineData("first.jpg", "video.photos")]
    [InlineData("notes.md", "markup.docx")]
    [InlineData("notes.md", "markup.epub")]
    [InlineData("notes.txt", "markup.epub")]
    [InlineData("article.html", "markup.md")]
    [InlineData("paper.docx", "markup.md")]
    [InlineData("book.epub", "book.azw3")]
    [InlineData("book.epub", "book.pdf")]
    [InlineData("book.epub", "markup.txt")]
    [InlineData("book.epub", "markup.docx")]
    [InlineData("book.xls", "sheet.xlsx")]
    [InlineData("book.xlsx", "sheet.csv")]
    [InlineData("book.ods", "sheet.pdf")]
    [InlineData("data.csv", "sheet.xlsx")]
    [InlineData("data.json", "table.parquet")]
    [InlineData("data.parquet", "table.csv")]
    public void New_conversions_are_available_from_the_wheel(string name, string id)
    {
        var asset = FileAsset.Create(Path.Combine(Path.GetTempPath(), name), 1, DateTimeOffset.UtcNow);
        Assert.Contains(new ActionCatalog().For(asset, ActionGroup.Convert), action => action.Id == id && action.Available);
    }
}
