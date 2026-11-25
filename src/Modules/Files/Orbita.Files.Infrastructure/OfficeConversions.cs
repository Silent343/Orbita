using System.IO.Compression;
using System.Xml;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

internal static class OfficeConversions
{
    public static async Task<IReadOnlyList<string>> ConvertAsync(ActionRequest request, ConversionWork work, CancellationToken token)
    {
        var word = request.ActionId == "word.pdf";
        var error = word ? "ErrorWordPdf" : "ErrorSpreadsheetConversion";
        var tool = ConversionWork.FindTool("ORBITA_LIBREOFFICE", "soffice.com", "ErrorLibreOfficeMissing",
            ConversionWork.LocalTool("libreoffice", "program", "soffice.com"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.com"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.com"));
        var source = await work.StageAsync(request.File.Path, "result" + request.File.Extension, token);
        var inputFilter = request.File.Extension switch
        {
            ".docx" => "Office Open XML Text",
            ".xlsx" => "Calc Office Open XML",
            ".xls" => "MS Excel 97",
            ".ods" => "calc8",
            ".csv" => $"Text - txt - csv (StarCalc):{CsvSeparator(source)},34,76,1,,0,true,false,true,false,false,0,false,false,false",
            _ => throw new DomainException(error)
        };
        if (request.File.Extension is ".docx" or ".xlsx")
            CheckOpenXml(source, word ? "word/document.xml" : "xl/workbook.xml", error, token);
        var (extension, filter) = request.ActionId switch
        {
            "word.pdf" => (".pdf", "pdf:writer_pdf_Export"),
            "sheet.pdf" => (".pdf", "pdf:calc_pdf_Export"),
            "sheet.xlsx" => (".xlsx", "xlsx:Calc Office Open XML"),
            "sheet.csv" => (".csv", "csv:Text - txt - csv (StarCalc):44,34,76,1,,0,false,true,true,false,false,-1,false,true"),
            _ => throw new DomainException(error)
        };
        await work.RunAsync(tool, [$"-env:UserInstallation={new Uri(PrepareProfile(work)).AbsoluteUri}",
            "--headless", "--norestore", $"--infilter={inputFilter}", "--convert-to", filter, "--outdir", work.DirectoryPath, source],
            error, token);
        if (request.ActionId != "sheet.csv") return [work.RequireOutput(work.PathFor("result" + extension), error)];
        var sheets = Directory.EnumerateFiles(work.DirectoryPath, "result*.csv").Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return sheets.Length > 0 ? sheets : throw new DomainException(error);
    }

    private static int CsvSeparator(string path)
    {
        using var reader = File.OpenText(path);
        var counts = new int[3];
        var quoted = false;
        for (var index = 0; index < 65_536 && reader.Read() is var next && next >= 0; index++)
        {
            if (next == '"') quoted = !quoted;
            if (quoted) continue;
            if (next is '\n' or '\r') break;
            if (next == ',') counts[0]++;
            if (next == ';') counts[1]++;
            if (next == '\t') counts[2]++;
        }
        return new[] { 44, 59, 9 }[Array.IndexOf(counts, counts.Max())];
    }

    internal static void CheckOpenXml(string path, string documentEntry, string error, CancellationToken token)
    {
        try
        {
            using var package = ZipFile.OpenRead(path);
            if (package.GetEntry(documentEntry) is null) throw new DomainException(error);
            foreach (var entry in package.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (!entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
                using var stream = entry.Open();
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000
                });
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship") continue;
                    var type = reader.GetAttribute("Type") ?? "";
                    if (type.EndsWith("/vbaProject", StringComparison.OrdinalIgnoreCase)
                        || type.EndsWith("/oleObject", StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)
                            && !type.EndsWith("/hyperlink", StringComparison.Ordinal)))
                        throw new DomainException("ErrorWordExternalContent");
                }
            }
        }
        catch (InvalidDataException) { throw new DomainException(error); }
        catch (XmlException) { throw new DomainException(error); }
    }

    private static string PrepareProfile(ConversionWork work)
    {
        var profile = work.PathFor("profile");
        var user = Directory.CreateDirectory(Path.Combine(profile, "user"));
        File.WriteAllText(Path.Combine(user.FullName, "registrymodifications.xcu"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <oor:items xmlns:oor="http://openoffice.org/2001/registry">
              <item oor:path="/org.openoffice.Office.Common/Security/Scripting">
                <prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop>
              </item>
              <item oor:path="/org.openoffice.Office.Calc/Content/Update">
                <prop oor:name="Link" oor:op="fuse"><value>1</value></prop>
              </item>
            </oor:items>
            """);
        return profile;
    }
}
