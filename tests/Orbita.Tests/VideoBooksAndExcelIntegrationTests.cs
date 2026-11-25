using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using ImageMagick;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.Files.Infrastructure;
using Xunit;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace Orbita.Tests;

public sealed partial class VideoBooksAndExcelTests
{
    [ConversionEngineFact("ORBITA_FFMPEG")]
    public async Task Media_exports_playable_mp4_webm_gif_frames_and_photo_sequence()
    {
        using var work = new TemporaryWorkspace();
        var mov = work.FilePath("Vídeo prueba.mov");
        var ffmpeg = Environment.GetEnvironmentVariable("ORBITA_FFMPEG")!;
        await RunAsync(ffmpeg, "-v", "error", "-f", "lavfi", "-i", "testsrc2=s=96x64:r=12",
            "-f", "lavfi", "-i", "sine=frequency=440", "-t", "2", "-c:v", "mpeg4", "-c:a", "aac", mov);
        var mp4 = (await ConvertAsync(mov, "video.mp4", work.Root)).OutputPaths.Single();
        var webm = (await ConvertAsync(mp4, "video.webm", work.Root)).OutputPaths.Single();
        foreach (var movie in new[] { mp4, webm })
            await RunAsync(ffmpeg, "-v", "error", "-xerror", "-i", movie, "-map", "0:v:0", "-map", "0:a:0", "-f", "null", "-");
        var mp3 = (await ConvertAsync(mov, "video.mp3", work.Root)).OutputPaths.Single();
        await RunAsync(ffmpeg, "-v", "error", "-xerror", "-i", mp3, "-map", "0:a:0", "-f", "null", "-");
        var gif = (await ConvertAsync(mov, "video.gif", work.Root)).OutputPaths.Single();
        using (var animation = new MagickImageCollection(gif)) Assert.True(animation.Count > 4);
        var frames = await ConvertAsync(mov, "video.frames", work.Root);
        Assert.Equal(2, frames.OutputPaths.Count);
        using (var first = new MagickImage(frames.OutputPaths[0])) Assert.Equal(96u, first.Width);

        var photo = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ocr-page.png");
        var second = Path.Combine(AppContext.BaseDirectory, "Fixtures", "blue-48x32.avif");
        var slideshow = (await ConvertAsync(photo, "video.photos", work.Root, [second])).OutputPaths.Single();
        await RunAsync(ffmpeg, "-v", "error", "-xerror", "-i", slideshow, "-map", "0:v:0", "-f", "null", "-");
    }

    [ConversionEngineFact("ORBITA_PANDOC")]
    public async Task Pandoc_keeps_structure_across_markdown_word_html_and_epub()
    {
        using var work = new TemporaryWorkspace();
        var md = work.FilePath("Lectura.md");
        await File.WriteAllTextAsync(md, "# Prueba de lectura\n\nTexto con **énfasis**.\n");
        var html = (await ConvertAsync(md, "markup.html", work.Root)).OutputPaths.Single();
        Assert.Contains("<strong>énfasis</strong>", await File.ReadAllTextAsync(html));
        var translated = (await ConvertAsync(html, "markup.md", work.Root)).OutputPaths.Single();
        Assert.Contains("**énfasis**", await File.ReadAllTextAsync(translated));
        var docx = (await ConvertAsync(md, "markup.docx", work.Root)).OutputPaths.Single();
        using (var doc = WordprocessingDocument.Open(docx, false))
            Assert.Contains("Prueba de lectura", doc.MainDocumentPart!.Document!.InnerText);
        var restored = (await ConvertAsync(docx, "markup.md", work.Root)).OutputPaths.Single();
        Assert.Contains("**énfasis**", await File.ReadAllTextAsync(restored));
        var epub = (await ConvertAsync(md, "markup.epub", work.Root)).OutputPaths.Single();
        using (var book = ZipFile.OpenRead(epub)) Assert.NotNull(book.GetEntry("META-INF/container.xml"));
        var txt = (await ConvertAsync(epub, "markup.txt", work.Root)).OutputPaths.Single();
        Assert.Contains("Texto con énfasis", await File.ReadAllTextAsync(txt));
        var bookWord = (await ConvertAsync(epub, "markup.docx", work.Root)).OutputPaths.Single();
        using (var doc = WordprocessingDocument.Open(bookWord, false))
            Assert.Contains("Texto con énfasis", doc.MainDocumentPart!.Document!.InnerText);

        var plain = work.FilePath("Texto.txt");
        await File.WriteAllTextAsync(plain, "# Texto & <ángulos>\nOtra línea");
        var plainWord = (await ConvertAsync(plain, "markup.docx", work.Root)).OutputPaths.Single();
        using (var doc = WordprocessingDocument.Open(plainWord, false))
        {
            var body = doc.MainDocumentPart!.Document!.Body!;
            Assert.Contains("# Texto & <ángulos>", body.InnerText);
            Assert.Contains("Otra línea", body.InnerText);
            Assert.NotEmpty(body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Break>());
        }
    }

    [ConversionEngineFact("ORBITA_CALIBRE")]
    public async Task Calibre_exports_a_drm_free_epub_as_kindle_and_searchable_pdf()
    {
        using var work = new TemporaryWorkspace();
        var epub = work.FilePath("Libro pequeño.epub");
        using (var archive = ZipFile.Open(epub, ZipArchiveMode.Create))
        {
            Add(archive, "mimetype", "application/epub+zip");
            Add(archive, "META-INF/container.xml", """
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="book.opf" media-type="application/oebps-package+xml"/></rootfiles>
                </container>
                """);
            Add(archive, "book.opf", """
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">orbita-test-ebook</dc:identifier>
                    <dc:title>Orbita Book</dc:title><dc:language>en</dc:language>
                    <meta property="dcterms:modified">2026-11-25T00:00:00Z</meta>
                  </metadata>
                  <manifest><item id="nav" href="chapter.xhtml" media-type="application/xhtml+xml" properties="nav"/></manifest>
                  <spine><itemref idref="nav"/></spine>
                </package>
                """);
            Add(archive, "chapter.xhtml", """
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
                  <head><title>Orbita Book</title></head><body>
                    <nav epub:type="toc"><ol><li><a href="#c1">Chapter</a></li></ol></nav>
                    <h1 id="c1">The first chapter</h1><p>Text of the local book.</p>
                  </body>
                </html>
                """);
        }
        var azw = (await ConvertAsync(epub, "book.azw3", work.Root)).OutputPaths.Single();
        Assert.Equal("BOOKMOBI", Encoding.ASCII.GetString(await File.ReadAllBytesAsync(azw), 60, 8));
        var pdfPath = (await ConvertAsync(epub, "book.pdf", work.Root)).OutputPaths.Single();
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        Assert.Contains("first chapter", string.Join(" ", pdf.GetPages().Select(page => page.Text)),
            StringComparison.OrdinalIgnoreCase);
    }

    [ConversionEngineFact("ORBITA_LIBREOFFICE")]
    public async Task Calc_writes_one_csv_per_sheet_and_imports_formula_strings_as_text()
    {
        using var work = new TemporaryWorkspace();
        var input = work.FilePath("Registro.xlsx");
        using (var package = SpreadsheetDocument.Create(input, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var workbook = package.AddWorkbookPart();
            var sheets = new S.Sheets();
            workbook.Workbook = new S.Workbook(sheets);
            foreach (var (name, value, index) in new[] { ("Ventas", "Lima", 1u), ("Compras", "Arequipa", 2u) })
            {
                var worksheet = workbook.AddNewPart<WorksheetPart>();
                worksheet.Worksheet = new S.Worksheet(new S.SheetData(new S.Row(new S.Cell(new S.InlineString(new S.Text(value)))
                    { DataType = S.CellValues.InlineString })));
                sheets.Append(new S.Sheet { Id = workbook.GetIdOfPart(worksheet), Name = name, SheetId = index });
            }
        }
        var sheetsResult = await ConvertAsync(input, "sheet.csv", work.Root);
        Assert.Equal(2, sheetsResult.OutputPaths.Count);
        Assert.Contains("Arequipa", await File.ReadAllTextAsync(sheetsResult.OutputPaths.Single(path => path.EndsWith("-Compras.csv"))));
        Assert.Contains("Lima", await File.ReadAllTextAsync(sheetsResult.OutputPaths.Single(path => path.EndsWith("-Ventas.csv"))));
        var pdfPath = (await ConvertAsync(input, "sheet.pdf", work.Root)).OutputPaths.Single();
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfPath)) Assert.True(pdf.NumberOfPages >= 1);

        var csv = work.FilePath("Datos.csv");
        await File.WriteAllTextAsync(csv, "Nombre;Dato\r\nVisita;=2+2\r\n");
        var converted = (await ConvertAsync(csv, "sheet.xlsx", work.Root)).OutputPaths.Single();
        using var xlsx = SpreadsheetDocument.Open(converted, false);
        Assert.Empty(xlsx.WorkbookPart!.WorksheetParts.SelectMany(part => part.Worksheet!.Descendants<S.CellFormula>()));
        Assert.Contains("=2+2", xlsx.WorkbookPart.SharedStringTablePart!.SharedStringTable!.InnerText);
    }

    [Fact]
    public async Task Table_formats_round_trip_with_utf8_and_leading_zeroes()
    {
        using var work = new TemporaryWorkspace();
        var csv = work.FilePath("tabla.csv");
        await File.WriteAllTextAsync(csv, "id,nombre\n001,Lucía\n002,Andrés\n");
        var parquet = (await ConvertAsync(csv, "table.parquet", work.Root)).OutputPaths.Single();
        Assert.Equal("PAR1", Encoding.ASCII.GetString(await File.ReadAllBytesAsync(parquet), 0, 4));
        var json = (await ConvertAsync(parquet, "table.json", work.Root)).OutputPaths.Single();
        using (var data = JsonDocument.Parse(await File.ReadAllTextAsync(json)))
        {
            Assert.Equal("001", data.RootElement[0].GetProperty("id").GetString());
            Assert.Equal("Lucía", data.RootElement[0].GetProperty("nombre").GetString());
        }
        var restored = (await ConvertAsync(json, "table.csv", work.Root)).OutputPaths.Single();
        Assert.Contains("002,Andrés", await File.ReadAllTextAsync(restored));
    }

    private static async Task<ActionResult> ConvertAsync(string path, string id, string output, IReadOnlyList<string>? photos = null)
    {
        var asset = await new LocalFileInspector().InspectAsync(path);
        return await new FileActions(new ActionCatalog(), [new ExternalConversionHandler(), new TableConversionHandler()])
            .ExecuteAsync(new ActionRequest(asset, id, output, AdditionalImages: photos));
    }

    private static async Task RunAsync(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }

    private static void Add(ZipArchive archive, string name, string contents)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents);
    }
}
