using Orbita.Files.Domain;

namespace Orbita.Files.Application;

public sealed class ActionCatalog : IActionCatalog
{
    public IReadOnlyList<FileAction> For(FileAsset file, ActionGroup group)
    {
        if (group == ActionGroup.Tools)
        {
            var tools = new List<FileAction> { new("zip", "ActionZip", "\uE8B7", group) };
            if (file.Category == FileCategory.Image && !file.IsHeic)
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
            FileCategory.Image when file.IsHeic => [
                new("image.jpg", "ActionJpg", "\uE91B", group),
                new("image.pdf", "ActionPhotosPdf", "\uEA90", group)],
            FileCategory.Image => [
                new("image.png", "ActionPng", "\uEB9F", group),
                new("image.jpg", "ActionJpg", "\uE91B", group),
                new("image.bmp", "ActionBmp", "\uE8B9", group),
                new("image.tiff", "ActionTiff", "\uE8A7", group),
                new("image.pdf", "ActionPhotosPdf", "\uEA90", group)],
            FileCategory.Word => [new("word.pdf", "ActionPdf", "\uEA90", group)],
            FileCategory.Video => [new("video.mp3", "ActionMp3", "\uE8D6", group)],
            FileCategory.Text => [
                new("text.txt", "ActionTxt", "\uE8E9", group),
                new("text.md", "ActionMarkdown", "\uE943", group)],
            _ => []
        };
    }
}
