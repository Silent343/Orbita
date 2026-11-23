using ImageMagick;
using Orbita.SharedKernel;
using SkiaSharp;
using Svg.Skia;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Orbita.Files.Infrastructure;

internal static partial class SvgImageCodec
{
    public static void Write(string path, Stream output, MagickFormat format)
    {
        using var input = File.OpenRead(path);
        using var local = ReadLocalSvg(input);
        using var svg = new SKSvg();
        var picture = svg.Load(local) ?? throw new DomainException("ErrorInvalidImage");
        var bounds = picture.CullRect;
        if (!(bounds.Width > 0 && bounds.Height > 0 && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height)))
            throw new DomainException("ErrorInvalidImage");
        if (format == MagickFormat.Pdf)
        {
            var fit = Math.Min(0.75f, 14_400 / Math.Max(bounds.Width, bounds.Height));
            using var document = SKDocument.CreatePdf(output) ?? throw new DomainException("ErrorInvalidImage");
            var canvas = document.BeginPage(Math.Max(1, bounds.Width * fit), Math.Max(1, bounds.Height * fit));
            canvas.Scale(fit);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
            document.EndPage();
            document.Close();
            return;
        }
        var scale = Math.Min(1, 8192 / Math.Max(bounds.Width, bounds.Height));
        var encoding = format switch
        {
            MagickFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            MagickFormat.WebP => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };
        using var colorSpace = SKColorSpace.CreateSrgb();
        picture.ToImage(output, format == MagickFormat.Jpeg ? SKColors.White : SKColors.Transparent,
            encoding, 94, scale, scale, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);
    }

    private static MemoryStream ReadLocalSvg(Stream input)
    {
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 2_000_000
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException) { throw new DomainException("ErrorInvalidImage"); }
        XNamespace svg = "http://www.w3.org/2000/svg";
        if (document.Root?.Name != svg + "svg") throw new DomainException("ErrorInvalidImage");
        if (document.DescendantNodes().OfType<XProcessingInstruction>().Any())
            throw new DomainException("ErrorSvgExternalContent");
        foreach (var element in document.Root.DescendantsAndSelf())
        {
            if (element.Name.Namespace != svg || element.Name.LocalName is "script" or "foreignObject")
                throw new DomainException("ErrorSvgExternalContent");
            if (element.Name.LocalName == "style") CheckLocalCss(element.Value);
            foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                var name = attribute.Name.LocalName;
                if (attribute.Name == XNamespace.Xml + "base" || name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                    throw new DomainException("ErrorSvgExternalContent");
                if (name is "href" or "src")
                {
                    if (!LocalFragment().IsMatch(attribute.Value))
                        throw new DomainException("ErrorSvgExternalContent");
                }
                else
                    CheckLocalCss(attribute.Value);
            }
        }
        var bytes = new MemoryStream();
        document.Save(bytes, SaveOptions.DisableFormatting);
        bytes.Position = 0;
        return bytes;
    }

    private static void CheckLocalCss(string value)
    {
        if (value.Contains('\\') || value.Contains('@') || UrlCall().IsMatch(LocalCssUrl().Replace(value, "")))
            throw new DomainException("ErrorSvgExternalContent");
    }

    [GeneratedRegex(@"^#[^\s'""()]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalFragment();

    [GeneratedRegex(@"url\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlCall();

    [GeneratedRegex(@"url\s*\(\s*(['""]?)#[^\s'""()]+\1\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalCssUrl();
}
