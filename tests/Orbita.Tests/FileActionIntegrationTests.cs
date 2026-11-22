using System.IO.Compression;
using System.Text;
using Orbita.Files.Domain;
using Orbita.Files.Infrastructure;
using Orbita.SharedKernel;
using Windows.Graphics.Imaging;
using Xunit;

namespace Orbita.Tests;

public sealed class FileActionIntegrationTests
{
    [Fact]
    public async Task Zip_preserves_original_and_uses_a_new_name_on_collision()
    {
        using var directory = new TemporaryWorkspace();
        var input = directory.FilePath("notes.txt");
        await File.WriteAllTextAsync(input, "áéíóú · Orbita");
        var asset = await new LocalFileInspector().InspectAsync(input);
        var handler = new ArchiveActionHandler();
        var first = await handler.ExecuteAsync(new ActionRequest(asset, "zip", directory.Root), default);
        var second = await handler.ExecuteAsync(new ActionRequest(asset, "zip", directory.Root), default);
        Assert.NotEqual(first.OutputPaths[0], second.OutputPaths[0]);
        using var archive = ZipFile.OpenRead(first.OutputPaths[0]);
        using var reader = new StreamReader(archive.Entries.Single().Open());
        Assert.Equal("áéíóú · Orbita", await reader.ReadToEndAsync());
        Assert.Equal("áéíóú · Orbita", await File.ReadAllTextAsync(input));
    }

    [Fact]
    public async Task Cancellation_removes_partial_outputs()
    {
        using var directory = new TemporaryWorkspace();
        var input = directory.FilePath("notes.txt");
        await File.WriteAllTextAsync(input, "preserve me");
        var asset = await new LocalFileInspector().InspectAsync(input);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ArchiveActionHandler()
            .ExecuteAsync(new ActionRequest(asset, "zip", directory.Root), cancellation.Token));
        Assert.Empty(Directory.GetFiles(directory.Root, "*.zip"));
        Assert.True(File.Exists(input));
    }

    [Fact]
    public async Task Image_resize_crop_rotate_and_jpeg_conversion_produce_decodable_results()
    {
        using var directory = new TemporaryWorkspace();
        var source = directory.FilePath("source.png");
        await WriteImageAsync(source);
        var asset = await new LocalFileInspector().InspectAsync(source);
        var handler = new ImageActionHandler();
        var cases = new[]
        {
            ("image.resize", new ImageEditOptions(Width: 8, Height: 4), 8u, 4u),
            ("image.crop", new ImageEditOptions(Left: 2, Top: 1, CropWidth: 4, CropHeight: 3), 4u, 3u),
            ("image.rotate", (ImageEditOptions?)null, 8u, 16u),
            ("image.jpg", (ImageEditOptions?)null, 16u, 8u),
            ("image.png", (ImageEditOptions?)null, 16u, 8u),
            ("image.bmp", (ImageEditOptions?)null, 16u, 8u),
            ("image.tiff", (ImageEditOptions?)null, 16u, 8u),
            ("image.compress", (ImageEditOptions?)null, 16u, 8u)
        };
        foreach (var (action, options, expectedWidth, expectedHeight) in cases)
        {
            var result = await handler.ExecuteAsync(new ActionRequest(asset, action, directory.Root, options), default);
            using var stream = File.OpenRead(result.OutputPaths.Single());
            using var random = stream.AsRandomAccessStream();
            var decoded = await BitmapDecoder.CreateAsync(random);
            Assert.Equal(expectedWidth, decoded.PixelWidth);
            Assert.Equal(expectedHeight, decoded.PixelHeight);
        }
    }

    [Fact]
    public async Task Invalid_crop_does_not_write_output()
    {
        using var directory = new TemporaryWorkspace();
        var source = directory.FilePath("source.png");
        await WriteImageAsync(source);
        var asset = await new LocalFileInspector().InspectAsync(source);
        await Assert.ThrowsAsync<DomainException>(() => new ImageActionHandler().ExecuteAsync(
            new ActionRequest(asset, "image.crop", directory.Root, new ImageEditOptions(CropWidth: 100, CropHeight: 100)), default));
        Assert.Single(Directory.GetFiles(directory.Root));
    }

    [Fact]
    public async Task Pdf_text_extraction_and_page_rendering_create_real_outputs()
    {
        using var directory = new TemporaryWorkspace();
        var source = directory.FilePath("sample.pdf");
        await File.WriteAllBytesAsync(source, MakePdf());
        var asset = await new LocalFileInspector().InspectAsync(source);
        var handler = new PdfActionHandler();
        var text = await handler.ExecuteAsync(new ActionRequest(asset, "pdf.txt", directory.Root), default);
        Assert.Contains("Hello Orbita", await File.ReadAllTextAsync(text.OutputPaths.Single()));
        var markdown = await handler.ExecuteAsync(new ActionRequest(asset, "pdf.md", directory.Root), default);
        Assert.Contains("Hello Orbita", await File.ReadAllTextAsync(markdown.OutputPaths.Single()));
        var images = await handler.ExecuteAsync(new ActionRequest(asset, "pdf.png", directory.Root), default);
        Assert.Single(images.OutputPaths);
        using var stream = File.OpenRead(images.OutputPaths.Single());
        using var random = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(random);
        Assert.True(decoder.PixelWidth > 0 && decoder.PixelHeight > 0);
        var jpeg = await handler.ExecuteAsync(new ActionRequest(asset, "pdf.jpg", directory.Root), default);
        Assert.True(new FileInfo(jpeg.OutputPaths.Single()).Length > 0);
    }

    private static async Task WriteImageAsync(string path)
    {
        using var stream = File.Create(path);
        using var random = stream.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, random);
        var bytes = Enumerable.Range(0, 16 * 8).SelectMany(_ => new byte[] { 48, 128, 72, 255 }).ToArray();
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, 16, 8, 96, 96, bytes);
        await encoder.FlushAsync();
    }

    internal static byte[] MakePdf()
    {
        const string content = "BT /F1 24 Tf 72 720 Td (Hello Orbita) Tj ET";
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"
        ];
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(text.Length);
            text.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = text.Length;
        text.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append($"{offset:0000000000} 00000 n \n");
        text.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(text.ToString());
    }
}
