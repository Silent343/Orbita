using System.Text;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using NativePdf = Windows.Data.Pdf.PdfDocument;
using TextPdf = UglyToad.PdfPig.PdfDocument;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Orbita.Files.Infrastructure;

public sealed class PdfActionHandler : IFileActionHandler
{
    public bool CanHandle(string actionId) => actionId is "pdf.png" or "pdf.jpg" or "pdf.txt" or "pdf.md";

    public Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken) =>
        request.ActionId is "pdf.txt" or "pdf.md"
            ? ExtractTextAsync(request, cancellationToken) : RenderPagesAsync(request, cancellationToken);

    private static async Task<ActionResult> ExtractTextAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        using var output = OutputFile.Create(request.OutputDirectory, Path.GetFileNameWithoutExtension(request.File.Name),
            request.ActionId == "pdf.md" ? ".md" : ".txt");
        await Task.Run(async () =>
        {
            using var document = TextPdf.Open(request.File.Path);
            await using var writer = new StreamWriter(output.Stream, new UTF8Encoding(false), leaveOpen: true);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (page.Number > 1) await writer.WriteLineAsync(request.ActionId == "pdf.md" ? "\n---\n" : "\f");
                await writer.WriteLineAsync(ContentOrderTextExtractor.GetText(page).AsMemory(), cancellationToken);
            }
            await writer.FlushAsync(cancellationToken);
        }, cancellationToken);
        var length = output.Stream.Length;
        output.Commit();
        return new ActionResult([output.Path], length);
    }

    private static async Task<ActionResult> RenderPagesAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        var input = await StorageFile.GetFileFromPathAsync(request.File.Path).AsTask(cancellationToken);
        var document = await NativePdf.LoadFromFileAsync(input).AsTask(cancellationToken);
        var reservations = new List<OutputFile>();
        try
        {
            var extension = request.ActionId == "pdf.jpg" ? ".jpg" : ".png";
            for (uint index = 0; index < document.PageCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var page = document.GetPage(index);
                using var rendered = new InMemoryRandomAccessStream();
                var width = (uint)Math.Clamp(page.Size.Width * 2, 1, 4096);
                var height = (uint)Math.Max(1, page.Size.Height * width / page.Size.Width);
                await page.RenderToStreamAsync(rendered, new Windows.Data.Pdf.PdfPageRenderOptions
                    { DestinationWidth = width, DestinationHeight = height }).AsTask(cancellationToken);
                rendered.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(rendered).AsTask(cancellationToken);
                using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied).AsTask(cancellationToken);
                var output = OutputFile.Create(request.OutputDirectory,
                    $"{Path.GetFileNameWithoutExtension(request.File.Name)}-page-{index + 1:000}", extension);
                reservations.Add(output);
                using (var outputStream = output.Stream.AsRandomAccessStream())
                    await ImageActionHandler.EncodeAsync(bitmap, outputStream, extension, .94f, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var result = new ActionResult(reservations.Select(output => output.Path).ToArray(),
                reservations.Sum(output => new FileInfo(output.Path).Length));
            foreach (var output in reservations) output.Commit();
            return result;
        }
        finally { foreach (var output in reservations) output.Dispose(); }
    }
}
