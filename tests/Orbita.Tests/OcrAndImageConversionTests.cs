using DocumentFormat.OpenXml.Packaging;
using ImageMagick;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.Files.Infrastructure;
using Orbita.SharedKernel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Orbita.Tests;

public sealed class OcrAndImageConversionTests
{
    [Fact]
    public async Task Word_recognizes_an_image_only_page_and_preserves_native_text_in_the_same_pdf()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.FilePath("mixed.pdf");
        using (var native = PdfReader.Open(new MemoryStream(FileActionIntegrationTests.MakePdf()), PdfDocumentOpenMode.Import))
        using (var pdf = new PdfDocument())
        using (var scan = XImage.FromFile(Fixture("ocr-page.png")))
        {
            pdf.AddPage(native.Pages[0]);
            var page = pdf.AddPage();
            using (var canvas = XGraphics.FromPdfPage(page))
                canvas.DrawImage(scan, 25, 150, 550, 286);
            pdf.Save(source);
        }
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(source))
            Assert.Empty(pdf.GetPage(2).Text.Trim());

        var asset = await new LocalFileInspector().InspectAsync(source);
        var actions = new FileActions(new ActionCatalog(), [new PdfActionHandler()]);
        var result = await actions.ExecuteAsync(new ActionRequest(asset, "pdf.word", workspace.Root));
        using var document = WordprocessingDocument.Open(result.OutputPaths.Single(), false);
        var body = document.MainDocumentPart!.Document!.Body!;
        Assert.Contains("Hello Orbita", body.InnerText);
        Assert.Contains("Factura", body.InnerText);
        Assert.Contains("9742", body.InnerText);
        Assert.Contains("España", body.InnerText);
        Assert.Single(body.Descendants<W.Break>());
        Assert.Empty(body.Descendants<W.Drawing>());
    }

    [Theory]
    [InlineData("photo.heic", "image.png")]
    [InlineData("photo.avif", "image.webp")]
    [InlineData("photo.dng", "image.tiff")]
    [InlineData("draw.svg", "image.svg.pdf")]
    [InlineData("animation.gif", "image.gif.frames")]
    [InlineData("icon.png", "image.ico")]
    [InlineData("icon.ico", "image.png")]
    public void Expanded_formats_appear_in_the_conversion_wheel(string name, string id)
    {
        var asset = FileAsset.Create(Path.Combine(Path.GetTempPath(), name), 1, DateTimeOffset.UtcNow);
        Assert.Contains(new ActionCatalog().For(asset, ActionGroup.Convert), action => action.Id == id && action.Available);
    }

    [Theory]
    [InlineData("red-64x48.heic", "image.png", MagickFormat.Png)]
    [InlineData("blue-48x32.avif", "image.webp", MagickFormat.WebP)]
    [InlineData("raw-96x64.dng", "image.tiff", MagickFormat.Tiff)]
    public async Task Photos_export_decodable_files(string fixture, string action, MagickFormat expected)
    {
        using var workspace = new TemporaryWorkspace();
        var result = await ConvertImageAsync(Fixture(fixture), action, workspace.Root);
        using var image = new MagickImage(result.OutputPaths.Single());
        Assert.Equal(expected, image.Format);
        Assert.True(image.Width > 0 && image.Height > 0);
    }

    [Fact]
    public async Task Png_to_icon_creates_multiple_sizes_and_ico_to_png_uses_the_largest()
    {
        using var workspace = new TemporaryWorkspace();
        var icon = await ConvertImageAsync(Fixture("ocr-page.png"), "image.ico", workspace.Root);
        using var frames = new MagickImageCollection(icon.OutputPaths.Single());
        Assert.Equal(new uint[] { 16, 32, 48, 256 }, frames.Select(frame => frame.Width).Order().ToArray());
        var result = await ConvertImageAsync(icon.OutputPaths.Single(), "image.png", workspace.Root);
        using var png = new MagickImage(result.OutputPaths.Single());
        Assert.Equal(256u, png.Width);
        Assert.Equal(256u, png.Height);
    }

    [Fact]
    public async Task Gif_saves_animation_frames_as_separate_pngs()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.FilePath("animation.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 48, 32));
            frames.Add(new MagickImage(MagickColors.Blue, 48, 32));
            frames.Write(source, MagickFormat.Gif);
        }
        var result = await ConvertImageAsync(source, "image.gif.frames", workspace.Root);
        Assert.Equal(2, result.OutputPaths.Count);
        using var first = new MagickImage(result.OutputPaths[0]);
        using var last = new MagickImage(result.OutputPaths[1]);
        Assert.Equal(48u, first.Width);
        Assert.Equal(32u, last.Height);
        using var firstPixels = first.GetPixels();
        using var lastPixels = last.GetPixels();
        Assert.NotEqual(firstPixels.GetPixel(10, 10).ToColor(), lastPixels.GetPixel(10, 10).ToColor());
    }

    [Fact]
    public async Task Svg_renders_to_standard_formats_and_checks_linked_resources()
    {
        using var workspace = new TemporaryWorkspace();
        var svg = workspace.FilePath("drawing.svg");
        await File.WriteAllTextAsync(svg, """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40" viewBox="0 0 80 40">
              <defs><linearGradient id="g"><stop offset="0" stop-color="#458caa"/><stop offset="1" stop-color="#025876"/></linearGradient></defs>
              <rect width="80" height="40" fill="url(#g)"/>
            </svg>
            """);
        foreach (var (action, format) in new[] { ("image.png", MagickFormat.Png), ("image.webp", MagickFormat.WebP), ("image.jpg", MagickFormat.Jpeg) })
        {
            var result = await ConvertImageAsync(svg, action, workspace.Root);
            using var raster = new MagickImage(result.OutputPaths.Single());
            Assert.Equal(format, raster.Format);
            Assert.Equal(80u, raster.Width);
            Assert.Equal(40u, raster.Height);
        }
        var outputPdf = await ConvertImageAsync(svg, "image.svg.pdf", workspace.Root);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(outputPdf.OutputPaths.Single());
        Assert.Equal(60, pdf.GetPage(1).Width);
        Assert.Equal(30, pdf.GetPage(1).Height);
        Assert.Empty(pdf.GetPage(1).GetImages());

        var linked = workspace.FilePath("linked.svg");
        await File.WriteAllTextAsync(linked, """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <image href="https://example.invalid/photo.png" width="80" height="40"/>
            </svg>
            """);
        var error = await Assert.ThrowsAsync<DomainException>(() => ConvertImageAsync(linked, "image.png", workspace.Root));
        Assert.Equal("ErrorSvgExternalContent", error.ResourceKey);
        Assert.False(File.Exists(workspace.FilePath("linked.png")));
    }

    private static async Task<ActionResult> ConvertImageAsync(string source, string action, string output)
    {
        var asset = await new LocalFileInspector().InspectAsync(source);
        return await new FileActions(new ActionCatalog(), [new ImageActionHandler()])
            .ExecuteAsync(new ActionRequest(asset, action, output));
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
