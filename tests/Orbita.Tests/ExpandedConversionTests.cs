using System.IO.Compression;
using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ImageMagick;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.Files.Infrastructure;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Orbita.Tests;

public sealed class ExpandedConversionTests
{
    [Theory]
    [InlineData("sample.pdf", "pdf.word")]
    [InlineData("sample.pdf", "pdf.pptx")]
    [InlineData("sample.docx", "word.pdf")]
    [InlineData("sample.heic", "image.jpg")]
    [InlineData("sample.png", "image.pdf")]
    [InlineData("sample.mp4", "video.mp3")]
    public void Conversions_are_available_in_the_wheel_catalog(string name, string actionId)
    {
        var source = FileAsset.Create(Path.Combine(Path.GetTempPath(), name), 1, DateTimeOffset.UtcNow);
        Assert.Contains(new ActionCatalog().For(source, ActionGroup.Convert),
            action => action.Id == actionId && action.Available);
    }

    [Fact]
    public async Task Pdf_converts_to_editable_text_and_a_presentation_with_page_images()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.FilePath("original.pdf");
        await File.WriteAllBytesAsync(source, FileActionIntegrationTests.MakePdf());
        var asset = await new LocalFileInspector().InspectAsync(source);
        var actions = new FileActions(new ActionCatalog(), [new PdfActionHandler()]);

        var word = await actions.ExecuteAsync(new ActionRequest(asset, "pdf.word", workspace.Root));
        using var wordDocument = WordprocessingDocument.Open(word.OutputPaths.Single(), false);
        Assert.Empty(new OpenXmlValidator().Validate(wordDocument));
        using var wordPackage = ZipFile.OpenRead(word.OutputPaths.Single());
        using var reader = new StreamReader(wordPackage.GetEntry("word/document.xml")!.Open());
        Assert.Contains("Hello Orbita", await reader.ReadToEndAsync());

        var slides = await actions.ExecuteAsync(new ActionRequest(asset, "pdf.pptx", workspace.Root));
        using var presentation = PresentationDocument.Open(slides.OutputPaths.Single(), false);
        var validation = new OpenXmlValidator().Validate(presentation).Select(error => error.Description).ToArray();
        Assert.Empty(validation);
        using var slidePackage = ZipFile.OpenRead(slides.OutputPaths.Single());
        Assert.NotNull(slidePackage.GetEntry("ppt/slides/slide1.xml"));
        Assert.Contains(slidePackage.Entries, entry => entry.FullName.StartsWith("ppt/media/") && entry.Length > 0);
    }

    [Fact]
    public async Task Heic_and_png_convert_to_real_jpeg_and_a_pdf_with_two_pages()
    {
        using var workspace = new TemporaryWorkspace();
        var heic = workspace.FilePath("foto.heic");
        var png = workspace.FilePath("second.png");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "red-64x48.heic"), heic);
        using (var sourceImage = new MagickImage(MagickColors.Crimson, 64, 48))
            sourceImage.Write(png, MagickFormat.Png);
        var asset = await new LocalFileInspector().InspectAsync(heic);
        var actions = new FileActions(new ActionCatalog(), [new ImageActionHandler(), new PhotoPdfActionHandler()]);
        var jpg = await actions.ExecuteAsync(new ActionRequest(asset, "image.jpg", workspace.Root));
        using var decoded = new MagickImage(jpg.OutputPaths.Single());
        Assert.Equal(MagickFormat.Jpeg, decoded.Format);
        Assert.Equal(64u, decoded.Width);
        Assert.Equal(48u, decoded.Height);

        var result = await actions.ExecuteAsync(new ActionRequest(asset, "image.pdf", workspace.Root,
            AdditionalImages: [png, heic]));
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(result.OutputPaths.Single());
        Assert.Equal(2, pdf.NumberOfPages);
        Assert.All(pdf.GetPages(), page => Assert.Single(page.GetImages()));
    }

    [ConversionEngineFact("ORBITA_LIBREOFFICE")]
    public async Task LibreOffice_exports_a_word_document_to_a_readable_pdf()
    {
        using var workspace = new TemporaryWorkspace();
        var path = workspace.FilePath("Documento con espacios.docx");
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            doc.AddMainDocumentPart().Document = new W.Document(
                new W.Body(new W.Paragraph(new W.Run(new W.Text("Prueba de Orbita")))));
        var asset = await new LocalFileInspector().InspectAsync(path);
        var actions = new FileActions(new ActionCatalog(), [new ExternalConversionHandler()]);
        var result = await actions.ExecuteAsync(new ActionRequest(asset, "word.pdf", workspace.Root));
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(result.OutputPaths.Single());
        Assert.Contains("Prueba de Orbita", pdf.GetPage(1).Text);
    }

    [ConversionEngineFact("ORBITA_FFMPEG")]
    public async Task Ffmpeg_extracts_an_mp3_audio_track_from_an_mp4_video()
    {
        using var workspace = new TemporaryWorkspace();
        var path = workspace.FilePath("Vídeo con espacios.mp4");
        var ffmpeg = Environment.GetEnvironmentVariable("ORBITA_FFMPEG")!;
        await RunFfmpeg(ffmpeg, "-v", "error", "-f", "lavfi", "-i", "color=blue:s=64x64",
            "-f", "lavfi", "-i", "sine=frequency=440", "-t", "1", "-c:v", "mpeg4", "-c:a", "aac", path);
        var asset = await new LocalFileInspector().InspectAsync(path);
        var actions = new FileActions(new ActionCatalog(), [new ExternalConversionHandler()]);
        var result = await actions.ExecuteAsync(new ActionRequest(asset, "video.mp3", workspace.Root));
        var mp3 = result.OutputPaths.Single();
        Assert.Equal(".mp3", Path.GetExtension(mp3));
        Assert.True(result.OutputBytes > 1_000);
        await RunFfmpeg(ffmpeg, "-v", "error", "-f", "mp3", "-i", mp3, "-f", "null", "-");
    }

    private static async Task RunFfmpeg(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }
}

public sealed class ConversionEngineFactAttribute : FactAttribute
{
    public ConversionEngineFactAttribute(string variable)
    {
        if (!File.Exists(Environment.GetEnvironmentVariable(variable)))
            Skip = $"Set {variable} to run the external conversion integration test.";
    }
}
