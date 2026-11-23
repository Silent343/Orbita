using ImageMagick;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.SharedKernel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Orbita.Files.Infrastructure;

public sealed class PhotoPdfActionHandler : IFileActionHandler
{
    private const int MaxPhotos = 100;
    public bool CanHandle(string actionId) => actionId == "image.pdf";

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        var paths = new[] { request.File.Path }.Concat(request.AdditionalImages ?? [])
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxPhotos + 1).ToArray();
        if (paths.Length > MaxPhotos) throw new DomainException("ErrorTooManyPhotos");
        using var document = new PdfDocument();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var asset = await new LocalFileInspector().InspectAsync(path, cancellationToken);
            if (asset.Category != FileCategory.Image) throw new DomainException("ErrorUnsupportedPhoto");
            using var jpeg = new MemoryStream();
            if (asset.IsHeic)
                await HeicJpeg.WriteAsync(path, jpeg, cancellationToken);
            else
            {
                await using var source = File.OpenRead(path);
                using var input = source.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(input).AsTask(cancellationToken);
                var scale = Math.Min(1, 4096d / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
                var transform = new BitmapTransform
                {
                    ScaledWidth = (uint)Math.Max(1, decoder.PixelWidth * scale),
                    ScaledHeight = (uint)Math.Max(1, decoder.PixelHeight * scale),
                    InterpolationMode = BitmapInterpolationMode.Fant
                };
                using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb).AsTask(cancellationToken);
                using var encoded = new InMemoryRandomAccessStream();
                await ImageActionHandler.EncodeAsync(bitmap, encoded, ".jpg", .9f, cancellationToken);
                encoded.Seek(0);
                using var bytes = encoded.AsStreamForRead();
                await bytes.CopyToAsync(jpeg, cancellationToken);
            }
            jpeg.Position = 0;
            AddPage(document, jpeg);
        }

        using var output = OutputFile.Create(request.OutputDirectory, Path.GetFileNameWithoutExtension(request.File.Name), ".pdf");
        await Task.Run(() => document.Save(output.Stream, false), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var length = output.Stream.Length;
        output.Commit();
        return new ActionResult([output.Path], length);
    }

    private static void AddPage(PdfDocument document, Stream jpeg)
    {
        using var image = XImage.FromStream(jpeg);
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        if (image.PixelWidth > image.PixelHeight) page.Orientation = PdfSharp.PageOrientation.Landscape;
        const double margin = 18;
        var scale = Math.Min((page.Width.Point - 2 * margin) / image.PixelWidth,
            (page.Height.Point - 2 * margin) / image.PixelHeight);
        var width = image.PixelWidth * scale;
        var height = image.PixelHeight * scale;
        using var canvas = XGraphics.FromPdfPage(page);
        canvas.DrawImage(image, (page.Width.Point - width) / 2, (page.Height.Point - height) / 2, width, height);
    }
}

internal static class HeicJpeg
{
    public static Task WriteAsync(string path, Stream output, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            using var source = File.OpenRead(path);
            try
            {
                var settings = new MagickReadSettings { Format = MagickFormat.Heic, FrameCount = 1 };
                using var image = new MagickImage(source, settings);
                cancellationToken.ThrowIfCancellationRequested();
                image.AutoOrient();
                image.ColorSpace = ColorSpace.sRGB;
                image.BackgroundColor = MagickColors.White;
                image.Alpha(AlphaAction.Remove);
                image.Strip();
                image.Quality = 94;
                image.Write(output, MagickFormat.Jpeg);
            }
            catch (MagickException) { throw new DomainException("ErrorInvalidHeic"); }
        }, cancellationToken);
}
