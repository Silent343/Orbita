using System.Text;
using ImageMagick;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

internal static class MediaConversions
{
    private const int MaxPhotos = 100;
    private static readonly string[] CommonArgs =
        ["-nostdin", "-hide_banner", "-loglevel", "error", "-n", "-protocol_whitelist", "file"];
    private static readonly string[] H264 = ["-c:v", "libx264", "-preset", "fast", "-crf", "22", "-pix_fmt", "yuv420p", "-movflags", "+faststart"];

    public static async Task<IReadOnlyList<string>> ConvertAsync(ActionRequest request, ConversionWork work, CancellationToken token)
    {
        var ffmpeg = ConversionWork.FindTool("ORBITA_FFMPEG", "ffmpeg.exe", "ErrorFfmpegMissing",
            ConversionWork.LocalTool("ffmpeg", "ffmpeg.exe"),
            ConversionWork.WinGetLink("ffmpeg.exe", Environment.SpecialFolder.LocalApplicationData),
            ConversionWork.WinGetLink("ffmpeg.exe", Environment.SpecialFolder.ProgramFiles));
        if (request.ActionId == "video.photos") return await PhotosAsync(request, work, ffmpeg, token);
        var format = VideoFormat(request.File.Extension);
        var input = await work.StageAsync(request.File.Path, "source" + request.File.Extension, token);
        var arguments = new List<string>(CommonArgs);
        if (format == "mov") arguments.AddRange(["-enable_drefs", "0", "-use_absolute_path", "0"]);
        arguments.AddRange(["-f", format, "-i", input, "-map_metadata", "-1", "-map_chapters", "-1"]);
        (string name, string[] options) = request.ActionId switch
        {
            "video.mp3" => ("result.mp3", new[] { "-map", "0:a:0", "-vn", "-sn", "-dn", "-c:a", "libmp3lame", "-q:a", "2" }),
            "video.mp4" => ("result.mp4", ["-map", "0:V:0", "-map", "0:a:0?", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
                .. H264, "-c:a", "aac", "-b:a", "160k"]),
            "video.webm" => ("result.webm", new[] { "-map", "0:V:0", "-map", "0:a:0?", "-c:v", "libvpx-vp9",
                "-crf", "32", "-b:v", "0", "-pix_fmt", "yuv420p", "-c:a", "libopus", "-b:a", "96k" }),
            "video.gif" => ("result.gif", new[] { "-filter_complex",
                "[0:V:0]trim=duration=10,fps=12,scale=w='min(720,iw)':h=-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse[out]",
                "-map", "[out]", "-loop", "0" }),
            "video.frames" => ("result-frame-%03d.png", new[] { "-map", "0:V:0", "-vf", "fps=1,scale=w='min(1280,iw)':h=-2",
                "-frames:v", "300", "-start_number", "1" }),
            _ => throw new DomainException("ErrorVideoConversion")
        };
        var output = work.PathFor(name);
        arguments.AddRange(options);
        arguments.Add(output);
        var error = request.ActionId == "video.mp3" ? "ErrorVideoMp3" : "ErrorVideoConversion";
        await work.RunAsync(ffmpeg, arguments, error, token);
        if (request.ActionId != "video.frames") return [work.RequireOutput(output, error)];
        var frames = Directory.EnumerateFiles(work.DirectoryPath, "result-frame-*.png").Order(StringComparer.Ordinal).ToArray();
        return frames.Length > 0 ? frames : throw new DomainException(error);
    }

    private static async Task<IReadOnlyList<string>> PhotosAsync(ActionRequest request, ConversionWork work,
        string ffmpeg, CancellationToken token)
    {
        var photos = new[] { request.File.Path }.Concat(request.AdditionalImages ?? [])
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxPhotos + 1).ToArray();
        if (photos.Length > MaxPhotos) throw new DomainException("ErrorTooManyPhotos");
        var manifest = await Task.Run(() =>
        {
            const int width = 1280;
            const int height = 720;
            var list = new StringBuilder("ffconcat version 1.0\n");
            for (var index = 0; index < photos.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var file = new FileInfo(photos[index]);
                if (!file.Exists) throw new DomainException("ErrorFileMissing");
                var asset = FileAsset.Create(file.FullName, file.Length, file.LastWriteTimeUtc);
                if (!asset.IsPhoto) throw new DomainException("ErrorUnsupportedPhoto");
                using var image = AdvancedImageCodec.Read(asset);
                AdvancedImageCodec.Prepare(image);
                image.Resize(width, height);
                image.Extent(width, height, Gravity.Center, MagickColors.Black);
                image.BackgroundColor = MagickColors.Black;
                image.Alpha(AlphaAction.Remove);
                var name = $"photo-{index:000}.png";
                image.Write(work.PathFor(name), MagickFormat.Png);
                list.Append("file ").Append(name).Append("\nduration 2\n");
            }
            list.Append($"file photo-{photos.Length - 1:000}.png\n");
            return list.ToString();
        }, token);
        var sequence = work.PathFor("sequence.ffconcat");
        await File.WriteAllTextAsync(sequence, manifest, token);
        var output = work.PathFor("result.mp4");
        await work.RunAsync(ffmpeg, [.. CommonArgs, "-f", "concat", "-safe", "1", "-i", sequence, "-r", "25",
            "-t", (photos.Length * 2).ToString(System.Globalization.CultureInfo.InvariantCulture), .. H264, output],
            "ErrorVideoConversion", token);
        return [work.RequireOutput(output, "ErrorVideoConversion")];
    }

    private static string VideoFormat(string extension) => extension switch
    {
        ".mp4" or ".m4v" or ".mov" or ".3gp" => "mov",
        ".mkv" or ".webm" => "matroska",
        ".avi" => "avi",
        ".wmv" => "asf",
        ".mpeg" or ".mpg" => "mpeg",
        _ => throw new DomainException("ErrorVideoConversion")
    };
}
