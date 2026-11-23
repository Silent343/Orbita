using System.Runtime.InteropServices.WindowsRuntime;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.SharedKernel;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Orbita.Files.Infrastructure;

public sealed class ImageActionHandler : IFileActionHandler
{
    public bool CanHandle(string actionId) => actionId is "image.jpg" or "image.png" or "image.bmp" or "image.tiff"
        or "image.crop" or "image.resize" or "image.rotate" or "image.compress";

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        if (request.File.IsHeic && request.ActionId == "image.jpg")
        {
            using var jpeg = OutputFile.Create(request.OutputDirectory, Path.GetFileNameWithoutExtension(request.File.Name), ".jpg");
            await HeicJpeg.WriteAsync(request.File.Path, jpeg.Stream, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = jpeg.Stream.Length;
            jpeg.Commit();
            return new ActionResult([jpeg.Path], bytes);
        }
        await using var input = File.OpenRead(request.File.Path);
        using var inputStream = input.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(inputStream).AsTask(cancellationToken);
        var transform = new BitmapTransform();
        var options = request.ImageOptions;
        if (request.ActionId == "image.resize")
        {
            if (options is null || options.Width is < 1 or > 16384 || options.Height is < 1 or > 16384)
                throw new DomainException("ErrorInvalidDimensions");
            transform.ScaledWidth = options.Width;
            transform.ScaledHeight = options.Height;
            transform.InterpolationMode = BitmapInterpolationMode.Fant;
        }
        if (request.ActionId == "image.crop")
        {
            if (options is null || options.CropWidth < 1 || options.CropHeight < 1
                || (ulong)options.Left + options.CropWidth > decoder.OrientedPixelWidth
                || (ulong)options.Top + options.CropHeight > decoder.OrientedPixelHeight)
                throw new DomainException("ErrorInvalidCrop");
            transform.Bounds = new BitmapBounds { X = options.Left, Y = options.Top,
                Width = options.CropWidth, Height = options.CropHeight };
        }
        if (request.ActionId == "image.rotate") transform.Rotation = BitmapRotation.Clockwise90Degrees;

        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb).AsTask(cancellationToken);
        var extension = request.ActionId switch
        {
            "image.jpg" or "image.compress" => ".jpg",
            "image.bmp" => ".bmp",
            "image.tiff" => ".tiff",
            _ => ".png"
        };
        var suffix = request.ActionId switch
        {
            "image.crop" => "-crop", "image.resize" => "-resize",
            "image.rotate" => "-rotated", "image.compress" => "-compressed", _ => ""
        };
        using var output = OutputFile.Create(request.OutputDirectory,
            Path.GetFileNameWithoutExtension(request.File.Name) + suffix, extension);
        using var randomOutput = output.Stream.AsRandomAccessStream();
        await EncodeAsync(bitmap, randomOutput, extension,
            request.ActionId == "image.compress" ? 0.75f : 0.94f, cancellationToken);
        var length = output.Stream.Length;
        output.Commit();
        return new ActionResult([output.Path], length);
    }

    internal static async Task EncodeAsync(SoftwareBitmap bitmap, IRandomAccessStream stream,
        string extension, float quality, CancellationToken cancellationToken)
    {
        var encoderId = extension switch
        {
            ".jpg" => BitmapEncoder.JpegEncoderId,
            ".bmp" => BitmapEncoder.BmpEncoderId,
            ".tiff" => BitmapEncoder.TiffEncoderId,
            _ => BitmapEncoder.PngEncoderId
        };
        var options = new BitmapPropertySet
        {
            ["ImageQuality"] = new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single)
        };
        var encoder = extension == ".jpg"
            ? await BitmapEncoder.CreateAsync(encoderId, stream, options).AsTask(cancellationToken)
            : await BitmapEncoder.CreateAsync(encoderId, stream).AsTask(cancellationToken);
        // Keep the composited pixels alive until FlushAsync has finished reading them.
        using var opaque = extension == ".jpg" ? FlattenOnWhite(bitmap) : null;
        encoder.SetSoftwareBitmap(opaque ?? bitmap);
        encoder.IsThumbnailGenerated = false;
        await encoder.FlushAsync().AsTask(cancellationToken);
    }

    private static SoftwareBitmap FlattenOnWhite(SoftwareBitmap source)
    {
        using var converted = SoftwareBitmap.Convert(source, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyToBuffer(pixels.AsBuffer());
        for (var index = 0; index < pixels.Length; index += 4)
        {
            var remaining = 255 - pixels[index + 3];
            pixels[index] = (byte)Math.Min(255, pixels[index] + remaining);
            pixels[index + 1] = (byte)Math.Min(255, pixels[index + 1] + remaining);
            pixels[index + 2] = (byte)Math.Min(255, pixels[index + 2] + remaining);
            pixels[index + 3] = 255;
        }
        var result = new SoftwareBitmap(BitmapPixelFormat.Bgra8, converted.PixelWidth, converted.PixelHeight, BitmapAlphaMode.Ignore);
        result.CopyFromBuffer(pixels.AsBuffer());
        return result;
    }
}
