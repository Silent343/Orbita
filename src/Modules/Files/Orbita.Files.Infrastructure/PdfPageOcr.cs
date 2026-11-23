using Orbita.SharedKernel;
using TesseractOCR;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using OcrImage = TesseractOCR.Pix.Image;

namespace Orbita.Files.Infrastructure;

internal sealed class PdfPageOcr(string source) : IDisposable
{
    private Engine? _engine;
    private PdfDocument? _document;

    public async Task<string> RecognizeAsync(int pageNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _engine ??= CreateEngine();
        _document ??= await PdfActionHandler.LoadNativeAsync(source, cancellationToken);
        using var page = _document.GetPage((uint)(pageNumber - 1));
        using var rendered = new InMemoryRandomAccessStream();
        await PdfActionHandler.RenderPageAsync(page, rendered, cancellationToken, requestedScale: 3);
        using var input = rendered.AsStreamForRead();
        using var png = new MemoryStream();
        await input.CopyToAsync(png, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var pixels = OcrImage.LoadFromMemory(png.GetBuffer(), 0, checked((int)png.Length));
        var dpi = (int)Math.Round(pixels.Width * 96 / page.Size.Width);
        pixels.XRes = pixels.YRes = Math.Clamp(dpi, 70, 288);
        using var result = _engine.Process(pixels);
        var text = result.Text;
        cancellationToken.ThrowIfCancellationRequested();
        return text ?? "";
    }

    private static Engine CreateEngine()
    {
        var data = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!File.Exists(Path.Combine(data, "spa.traineddata")) || !File.Exists(Path.Combine(data, "eng.traineddata")))
            throw new DomainException("ErrorOcrUnavailable");
        try { return new Engine(data, "spa+eng"); }
        catch (Exception error) when (error is TesseractOCR.Exceptions.TesseractException or DllNotFoundException or TypeInitializationException)
        {
            throw new DomainException("ErrorOcrUnavailable");
        }
    }

    public void Dispose() => _engine?.Dispose();
}
