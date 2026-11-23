using System.Diagnostics;
using System.IO.Compression;
using System.Xml;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

public sealed class ExternalConversionHandler : IFileActionHandler
{
    public bool CanHandle(string actionId) => actionId is "word.pdf" or "video.mp3";

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var word = request.ActionId == "word.pdf";
        var tool = word ? FindLibreOffice() : FindFfmpeg();
        if (tool is null) throw new DomainException(word ? "ErrorLibreOfficeMissing" : "ErrorFfmpegMissing");
        var work = Directory.CreateTempSubdirectory("Orbita-");
        try
        {
            var extension = word ? ".pdf" : ".mp3";
            var converted = Path.Combine(work.FullName, "converted" + extension);
            string[] arguments;
            if (word)
            {
                var staged = Path.Combine(work.FullName, "converted.docx");
                await using (var original = File.OpenRead(request.File.Path))
                await using (var copy = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await original.CopyToAsync(copy, cancellationToken);
                CheckDocumentRelationships(staged, cancellationToken);
                var profile = PrepareOfficeProfile(work.FullName);
                arguments = [$"-env:UserInstallation={new Uri(profile).AbsoluteUri}", "--headless", "--norestore",
                    "--infilter=Office Open XML Text", "--convert-to", "pdf:writer_pdf_Export", "--outdir", work.FullName, staged];
            }
            else
            {
                arguments = ["-nostdin", "-hide_banner", "-loglevel", "error", "-n",
                    "-protocol_whitelist", "file", "-f", VideoFormat(request.File.Extension), "-i", request.File.Path,
                    "-map", "0:a:0", "-map_metadata", "-1", "-vn", "-sn", "-dn", "-c:a", "libmp3lame", "-q:a", "2", converted];
            }
            await RunAsync(tool, work.FullName, arguments, word, cancellationToken);
            if (!File.Exists(converted) || new FileInfo(converted).Length == 0)
                throw new DomainException(word ? "ErrorWordPdf" : "ErrorVideoMp3");
            using var output = OutputFile.Create(request.OutputDirectory, Path.GetFileNameWithoutExtension(request.File.Name), extension);
            await using var result = File.OpenRead(converted);
            await result.CopyToAsync(output.Stream, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            output.Commit();
            return new ActionResult([output.Path], output.Stream.Length);
        }
        finally
        {
            try { work.Delete(recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string VideoFormat(string extension) => extension switch
    {
        ".mp4" or ".m4v" or ".mov" or ".3gp" => "mov",
        ".mkv" or ".webm" => "matroska",
        ".avi" => "avi",
        ".wmv" => "asf",
        ".mpeg" or ".mpg" => "mpeg",
        _ => throw new DomainException("ErrorVideoMp3")
    };

    private static string? FindLibreOffice() => FirstExisting("ORBITA_LIBREOFFICE", "soffice.com",
        Path.Combine(AppContext.BaseDirectory, "tools", "libreoffice", "program", "soffice.com"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.com"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.com"));

    private static string? FindFfmpeg() => FirstExisting("ORBITA_FFMPEG", "ffmpeg.exe",
        Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "ffmpeg.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinGet", "Links", "ffmpeg.exe"));

    private static string? FirstExisting(string variable, string fileName, params string[] defaults)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        return defaults.Prepend(configured).FirstOrDefault(path => !string.IsNullOrEmpty(path)
            && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase) && File.Exists(path));
    }

    private static void CheckDocumentRelationships(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var package = ZipFile.OpenRead(path);
            if (package.GetEntry("word/document.xml") is null) throw new DomainException("ErrorWordPdf");
            foreach (var entry in package.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
        catch (InvalidDataException) { throw new DomainException("ErrorWordPdf"); }
        catch (XmlException) { throw new DomainException("ErrorWordPdf"); }
    }

    private static string PrepareOfficeProfile(string directory)
    {
        var profile = Path.Combine(directory, "profile");
        var user = Directory.CreateDirectory(Path.Combine(profile, "user"));
        File.WriteAllText(Path.Combine(user.FullName, "registrymodifications.xcu"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <oor:items xmlns:oor="http://openoffice.org/2001/registry">
              <item oor:path="/org.openoffice.Office.Common/Security/Scripting">
                <prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop>
              </item>
            </oor:items>
            """);
        return profile;
    }

    private static async Task RunAsync(string tool, string directory, string[] arguments, bool word,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(tool) { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = directory };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        cancellationToken.ThrowIfCancellationRequested();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromMinutes(10));
        using var process = Process.Start(start) ?? throw new DomainException(word ? "ErrorWordPdf" : "ErrorVideoMp3");
        try { await process.WaitForExitAsync(limit.Token); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            cancellationToken.ThrowIfCancellationRequested();
            throw new DomainException("ErrorConversionTimeout");
        }
        if (process.ExitCode != 0) throw new DomainException(word ? "ErrorWordPdf" : "ErrorVideoMp3");
    }
}
