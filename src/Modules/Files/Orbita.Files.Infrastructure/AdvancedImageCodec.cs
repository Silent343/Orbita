using ImageMagick;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

internal static class AdvancedImageCodec
{
    internal const int MaxFrames = 300;

    static AdvancedImageCodec()
    {
        ResourceLimits.Width = ResourceLimits.Height = 16384;
        ResourceLimits.Memory = 256 * 1024 * 1024;
        ResourceLimits.Disk = 1024 * 1024 * 1024;
        ResourceLimits.ListLength = MaxFrames + 1;
    }

    public static IMagickImage<byte> Read(FileAsset file)
    {
        using var input = File.OpenRead(file.Path);
        try
        {
            if (file.Extension == ".ico")
            {
                using var icons = new MagickImageCollection(input, new MagickReadSettings { Format = MagickFormat.Ico });
                return icons.MaxBy(icon => (ulong)icon.Width * icon.Height)?.Clone()
                    ?? throw new DomainException("ErrorInvalidImage");
            }
            var settings = new MagickReadSettings { Format = InputFormat(file), FrameCount = 1 };
            return new MagickImage(input, settings);
        }
        catch (MagickException)
        {
            throw new DomainException(file.IsRawPhoto ? "ErrorInvalidRawImage" : "ErrorInvalidImage");
        }
    }

    public static MagickImageCollection ReadGif(FileAsset file)
    {
        using var input = File.OpenRead(file.Path);
        try
        {
            return new MagickImageCollection(input, new MagickReadSettings
            {
                Format = MagickFormat.Gif, FrameCount = MaxFrames + 1
            });
        }
        catch (MagickException) { throw new DomainException("ErrorInvalidImage"); }
    }

    public static void Prepare(IMagickImage<byte> image)
    {
        image.AutoOrient();
        image.ColorSpace = ColorSpace.sRGB;
        image.Strip();
    }

    public static void PrepareJpeg(IMagickImage<byte> image)
    {
        image.BackgroundColor = MagickColors.White;
        image.Alpha(AlphaAction.Remove);
        image.Quality = 94;
    }

    private static MagickFormat InputFormat(FileAsset file) => file.Extension switch
    {
        ".png" => MagickFormat.Png,
        ".jpg" or ".jpeg" => MagickFormat.Jpeg,
        ".bmp" => MagickFormat.Bmp,
        ".tif" or ".tiff" => MagickFormat.Tiff,
        ".heic" or ".heif" => MagickFormat.Heic,
        ".avif" => MagickFormat.Avif,
        ".webp" => MagickFormat.WebP,
        _ when file.IsRawPhoto => MagickFormat.Dng,
        _ => throw new DomainException("ErrorInvalidImage")
    };

}
