using Orbita.Files.Domain;

namespace Orbita.Files.Application;

public sealed class ActionCatalog : IActionCatalog
{
    public IReadOnlyList<FileAction> For(FileAsset file, ActionGroup group)
    {
        if (group == ActionGroup.Tools)
        {
            var tools = new List<FileAction> { new("zip", "ActionZip", "\uE8B7", group) };
            if (file.UsesWindowsBitmap)
                tools.AddRange([
                    new("image.crop", "ActionCrop", "\uE7A8", group),
                    new("image.resize", "ActionResize", "\uE740", group),
                    new("image.rotate", "ActionRotate", "\uE7AD", group),
                    new("image.compress", "ActionCompress", "\uE8B8", group)]);
            return tools;
        }

        return file.Category switch
        {
            FileCategory.Pdf => [
                new("pdf.word", "ActionWord", "\uE8A5", group),
                new("pdf.pptx", "ActionPowerPoint", "\uE9F9", group),
                new("pdf.txt", "ActionTxt", "\uE8E9", group),
                new("pdf.md", "ActionMarkdown", "\uE943", group),
                new("pdf.jpg", "ActionJpg", "\uE91B", group),
                new("pdf.png", "ActionPng", "\uEB9F", group)],
            FileCategory.Image => ImageConversions(file, group),
            FileCategory.Word => [new("word.pdf", "ActionPdf", "\uEA90", group),
                Convert("markup.md", "ActionMarkdown"), Convert("markup.html", "ActionHtml"),
                Convert("markup.epub", "ActionEpub"), Convert("markup.txt", "ActionTxt")],
            FileCategory.Video => VideoConversions(file),
            FileCategory.Text => TextConversions(file),
            FileCategory.Ebook => [Convert("book.azw3", "ActionAzw3"), Convert("book.pdf", "ActionPdf"),
                Convert("markup.docx", "ActionDocx"), Convert("markup.txt", "ActionTxt"), Convert("markup.html", "ActionHtml")],
            FileCategory.Spreadsheet => file.Extension == ".xlsx"
                ? [Convert("sheet.pdf", "ActionPdf"), Convert("sheet.csv", "ActionCsvSheets")]
                : [Convert("sheet.xlsx", "ActionXlsx"), Convert("sheet.pdf", "ActionPdf"), Convert("sheet.csv", "ActionCsvSheets")],
            FileCategory.Table => file.Extension switch
            {
                ".csv" => [Convert("sheet.xlsx", "ActionXlsx"), Convert("table.json", "ActionJson"), Convert("table.parquet", "ActionParquet")],
                ".json" => [Convert("table.csv", "ActionCsv"), Convert("table.parquet", "ActionParquet")],
                _ => [Convert("table.csv", "ActionCsv"), Convert("table.json", "ActionJson")]
            },
            _ => []
        };
    }

    private static IReadOnlyList<FileAction> ImageConversions(FileAsset file, ActionGroup group)
    {
        FileAction png = new("image.png", "ActionPng", "\uEB9F", group);
        FileAction jpg = new("image.jpg", "ActionJpg", "\uE91B", group);
        FileAction webp = new("image.webp", "ActionWebp", "\uEB9F", group);
        FileAction tiff = new("image.tiff", "ActionTiff", "\uE8A7", group);
        FileAction photosPdf = new("image.pdf", "ActionPhotosPdf", "\uEA90", group);
        var video = new FileAction("video.photos", "ActionPhotosVideo", "\uE714", group);
        return file.Extension switch
        {
            ".svg" => [png, webp, new("image.svg.pdf", "ActionSvgPdf", "\uEA90", group), jpg],
            ".gif" => [new("image.gif.frames", "ActionGifFrames", "\uE8B9", group)],
            ".ico" => [png],
            ".png" => [jpg, webp, new("image.ico", "ActionIco", "\uE7C5", group), photosPdf, video, png, tiff,
                new("image.bmp", "ActionBmp", "\uE8B9", group)],
            _ when file.UsesWindowsBitmap => [png, jpg, webp, photosPdf, video, tiff, new("image.bmp", "ActionBmp", "\uE8B9", group)],
            ".webp" => [png, jpg, tiff, photosPdf, video],
            _ => [jpg, png, webp, tiff, photosPdf, video]
        };
    }

    private static IReadOnlyList<FileAction> VideoConversions(FileAsset file)
    {
        var actions = new List<FileAction>();
        if (file.Extension != ".mp4") actions.Add(Convert("video.mp4", "ActionMp4"));
        if (file.Extension != ".webm") actions.Add(Convert("video.webm", "ActionWebm"));
        actions.AddRange([Convert("video.mp3", "ActionMp3"), Convert("video.gif", "ActionVideoGif"),
            Convert("video.frames", "ActionVideoFrames")]);
        return actions;
    }

    private static IReadOnlyList<FileAction> TextConversions(FileAsset file) => file.Extension switch
    {
        ".txt" => [Convert("text.txt", "ActionTxt"), Convert("text.md", "ActionMarkdown"),
            Convert("markup.docx", "ActionDocx"), Convert("markup.html", "ActionHtml"), Convert("markup.epub", "ActionEpub")],
        ".md" or ".markdown" => [Convert("markup.html", "ActionHtml"), Convert("markup.docx", "ActionDocx"),
            Convert("markup.epub", "ActionEpub"), Convert("markup.txt", "ActionTxt")],
        _ => [Convert("markup.md", "ActionMarkdown"), Convert("markup.docx", "ActionDocx"),
            Convert("markup.epub", "ActionEpub"), Convert("markup.txt", "ActionTxt")]
    };

    private static FileAction Convert(string id, string label) => new(id, label, "\uE8A5", ActionGroup.Convert);
}
