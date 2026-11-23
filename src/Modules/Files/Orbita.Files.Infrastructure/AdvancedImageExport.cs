using ImageMagick;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

internal static class AdvancedImageExport
{
    public static ActionResult Convert(ActionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ActionId == "image.gif.frames") return ExtractFrames(request, cancellationToken);
        var (extension, format) = request.ActionId switch
        {
            "image.jpg" => (".jpg", MagickFormat.Jpeg),
            "image.png" => (".png", MagickFormat.Png),
            "image.webp" => (".webp", MagickFormat.WebP),
            "image.tiff" => (".tiff", MagickFormat.Tiff),
            "image.ico" => (".ico", MagickFormat.Ico),
            "image.svg.pdf" => (".pdf", MagickFormat.Pdf),
            _ => throw new DomainException("ErrorActionUnavailable")
        };
        using var output = OutputFile.Create(request.OutputDirectory,
            Path.GetFileNameWithoutExtension(request.File.Name), extension);
        if (request.File.Extension == ".svg")
            SvgImageCodec.Write(request.File.Path, output.Stream, format);
        else
        {
            using var image = AdvancedImageCodec.Read(request.File);
            AdvancedImageCodec.Prepare(image);
            if (format == MagickFormat.Jpeg) AdvancedImageCodec.PrepareJpeg(image);
            if (format == MagickFormat.WebP) image.Quality = 90;
            if (format == MagickFormat.Ico) WriteIcon(image, output.Stream);
            else image.Write(output.Stream, format);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var length = output.Stream.Length;
        if (length == 0) throw new DomainException("ErrorInvalidImage");
        output.Commit();
        return new ActionResult([output.Path], length);
    }

    private static void WriteIcon(IMagickImage<byte> image, Stream destination)
    {
        using var versions = new MagickImageCollection();
        foreach (var size in new uint[] { 16, 32, 48, 256 })
        {
            var version = image.Clone();
            versions.Add(version);
            version.Resize(size, size);
            version.Extent(size, size, Gravity.Center, MagickColors.Transparent);
        }
        versions.Write(destination, MagickFormat.Ico);
    }

    private static ActionResult ExtractFrames(ActionRequest request, CancellationToken cancellationToken)
    {
        using var frames = AdvancedImageCodec.ReadGif(request.File);
        if (frames.Count == 0) throw new DomainException("ErrorInvalidImage");
        if (frames.Count > AdvancedImageCodec.MaxFrames) throw new DomainException("ErrorTooManyGifFrames");
        frames.Coalesce();
        var outputs = new List<OutputFile>();
        try
        {
            var stem = Path.GetFileNameWithoutExtension(request.File.Name);
            for (var i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var output = OutputFile.Create(request.OutputDirectory, $"{stem}-frame-{i + 1:000}", ".png");
                outputs.Add(output);
                frames[i].Write(output.Stream, MagickFormat.Png);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var result = new ActionResult(outputs.Select(output => output.Path).ToArray(), outputs.Sum(output => output.Stream.Length));
            foreach (var output in outputs) output.Commit();
            return result;
        }
        finally { foreach (var output in outputs) output.Dispose(); }
    }
}
