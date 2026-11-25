using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

internal static partial class BookConversions
{
    public static async Task<IReadOnlyList<string>> ConvertAsync(ActionRequest request, ConversionWork work, CancellationToken token)
    {
        var source = await work.StageAsync(request.File.Path, "source" + request.File.Extension, token);
        if (request.ActionId.StartsWith("book.", StringComparison.Ordinal))
        {
            var calibre = ConversionWork.FindTool("ORBITA_CALIBRE", "ebook-convert.exe", "ErrorCalibreMissing",
                ConversionWork.LocalTool("calibre", "ebook-convert.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Calibre2", "ebook-convert.exe"));
            CheckEpub(source, token);
            var book = work.PathFor(request.ActionId == "book.azw3" ? "result.azw3" : "result.pdf");
            await work.RunAsync(calibre, [source, book], "ErrorBookConversion", token,
                new Dictionary<string, string> { ["CALIBRE_CONFIG_DIRECTORY"] = work.PathFor("calibre-config") });
            return [work.RequireOutput(book, "ErrorBookConversion")];
        }

        var pandoc = ConversionWork.FindTool("ORBITA_PANDOC", "pandoc.exe", "ErrorPandocMissing",
            ConversionWork.LocalTool("pandoc", "pandoc.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Pandoc", "pandoc.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pandoc", "pandoc.exe"),
            ConversionWork.WinGetLink("pandoc.exe", Environment.SpecialFolder.LocalApplicationData),
            ConversionWork.WinGetLink("pandoc.exe", Environment.SpecialFolder.ProgramFiles));
        const string error = "ErrorTextConversion";
        if (request.File.Extension == ".docx") OfficeConversions.CheckOpenXml(source, "word/document.xml", error, token);
        if (request.File.Extension == ".epub") CheckEpub(source, token);
        if (request.File.Extension == ".txt")
        {
            var html = work.PathFor("source.html");
            using var input = File.OpenText(source);
            await using var markup = File.CreateText(html);
            await markup.WriteLineAsync("<p>".AsMemory(), token);
            while (await input.ReadLineAsync(token) is { } line)
                await markup.WriteLineAsync((WebUtility.HtmlEncode(line) + "<br>").AsMemory(), token);
            await markup.WriteLineAsync("</p>".AsMemory(), token);
            source = html;
        }
        var reader = request.File.Extension switch
        {
            ".md" or ".markdown" => "markdown-raw_html-raw_tex-raw_attribute",
            ".html" or ".htm" or ".txt" => "html-raw_html",
            ".docx" => "docx",
            ".epub" => "epub",
            _ => throw new DomainException(error)
        };
        var (extension, writer) = request.ActionId switch
        {
            "markup.html" => (".html", "html5"),
            "markup.md" => (".md", "gfm-raw_html"),
            "markup.docx" => (".docx", "docx"),
            "markup.epub" => (".epub", "epub3"),
            "markup.txt" => (".txt", "plain"),
            _ => throw new DomainException(error)
        };
        var output = work.PathFor("result" + extension);
        var arguments = new List<string> { "--sandbox", "--from", reader, "--to", writer, "--output", output };
        if (extension is ".html" or ".epub")
            arguments.AddRange(["--standalone", "--metadata", $"title={Path.GetFileNameWithoutExtension(request.File.Name)}"]);
        arguments.Add(source);
        await work.RunAsync(pandoc, arguments, error, token);
        return [work.RequireOutput(output, error)];
    }

    private static void CheckEpub(string path, CancellationToken token)
    {
        try
        {
            using var book = ZipFile.OpenRead(path);
            if (book.GetEntry("META-INF/container.xml") is null || book.Entries.Count > 10_000)
                throw new DomainException("ErrorBookConversion");
            if (book.Entries.Sum(entry => entry.Length) > 512L * 1024 * 1024)
                throw new DomainException("ErrorBookConversion");
            foreach (var entry in book.Entries)
            {
                token.ThrowIfCancellationRequested();
                var name = entry.FullName;
                if (name.StartsWith('/') || name.Contains('\\') || name.Contains(':') || name.Split('/').Contains(".."))
                    throw new DomainException("ErrorBookResources");
                var extension = Path.GetExtension(name).ToLowerInvariant();
                if (extension is ".css")
                {
                    if (entry.Length > 2_000_000) throw new DomainException("ErrorBookResources");
                    using var reader = new StreamReader(entry.Open());
                    CheckCss(reader.ReadToEnd(), name);
                }
                else if (extension is ".html" or ".htm" or ".xhtml" or ".svg" or ".opf" or ".ncx" or ".xml")
                    CheckMarkup(entry);
            }
        }
        catch (InvalidDataException) { throw new DomainException("ErrorBookConversion"); }
        catch (XmlException) { throw new DomainException("ErrorBookResources"); }
    }

    private static void CheckMarkup(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 8_000_000
        });
        var document = XDocument.Load(reader);
        if (document.DescendantNodes().OfType<XProcessingInstruction>().Any())
            throw new DomainException("ErrorBookResources");
        foreach (var node in document.Descendants())
        {
            var name = node.Name.LocalName.ToLowerInvariant();
            if (name is "script" or "iframe" or "object" or "embed" or "base" or "foreignobject")
                throw new DomainException("ErrorBookResources");
            if (name == "style") CheckCss(node.Value, entry.FullName);
            foreach (var attribute in node.Attributes().Where(attr => !attr.IsNamespaceDeclaration))
            {
                var attr = attribute.Name.LocalName.ToLowerInvariant();
                if (attr.StartsWith("on", StringComparison.Ordinal) || attr is "srcset" or "http-equiv"
                    || attribute.Name == XNamespace.Xml + "base")
                    throw new DomainException("ErrorBookResources");
                if ((attr is "src" or "poster" or "href" or "data") && !(attr == "href" && name == "a"))
                    CheckResource(attribute.Value, entry.FullName);
                if (attr is "style" or "fill" or "stroke" or "filter" or "mask")
                    CheckCss(attribute.Value, entry.FullName);
            }
        }
    }

    private static void CheckCss(string value, string name)
    {
        if (value.Contains('\\') || value.Contains("@import", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("ErrorBookResources");
        foreach (Match url in CssUrl().Matches(value))
            CheckResource(url.Groups[1].Value.Trim().Trim('"', '\''), name);
        if (CssUrlStart().IsMatch(CssUrl().Replace(value, "")))
            throw new DomainException("ErrorBookResources");
    }

    private static void CheckResource(string reference, string owner)
    {
        reference = Uri.UnescapeDataString(reference.Trim());
        if (reference.Length == 0 || reference.StartsWith('#')) return;
        var origin = new Uri("https://epub.invalid/content/" +
            string.Join('/', owner.Split('/').Select(Uri.EscapeDataString)));
        if (reference.Contains('\\') || reference.StartsWith('/') || Uri.TryCreate(reference, UriKind.Absolute, out _)
            || !Uri.TryCreate(origin, reference, out var target) || target.Host != origin.Host
            || !target.AbsolutePath.StartsWith("/content/", StringComparison.Ordinal))
            throw new DomainException("ErrorBookResources");
    }

    [GeneratedRegex(@"url\s*\(\s*([^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssUrl();

    [GeneratedRegex(@"url\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssUrlStart();
}
