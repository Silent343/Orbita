using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using Drawing = System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DrawingGradient = System.Drawing.Drawing2D.LinearGradientBrush;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Orbita.Desktop.Presentation;
using Orbita.Desktop.Presentation.Controls;
using Orbita.Desktop.WindowsIntegration;
using Orbita.Files.Domain;
using Orbita.Localization;
using Windows.Graphics.Imaging;

namespace Orbita.Desktop.Verification;

/// <summary>Opt-in UI smoke check, invoked only by --verify-ui OUTPUT_DIRECTORY.</summary>
internal static class UiVerification
{
    public static async Task RunAsync(MainWindow window, IServiceProvider services, string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new Dictionary<string, object>();
        try
        {
            await Task.Delay(800);
            var localizer = services.GetRequiredService<ILocalizer>();
            localizer.ChangeLanguage("es");
            foreach (var page in new[] { "workspace", "settings", "history" })
            {
                window.ShowPage(page);
                await Task.Delay(350);
                window.VerificationRoot.UpdateLayout();
                var texts = Descendants(window.VerificationRoot).OfType<TextBlock>().Select(text => text.Text).ToArray();
                var key = page switch { "workspace" => "WorkspaceTitle", "settings" => "SettingsTitle", _ => "HistoryTitle" };
                if (!texts.Contains(localizer[key])) throw new InvalidOperationException($"Missing translated title on {page}");
                results[$"{page}.es"] = true;
                localizer.ChangeLanguage("en");
                await Task.Delay(200);
                texts = Descendants(window.VerificationRoot).OfType<TextBlock>().Select(text => text.Text).ToArray();
                if (!texts.Contains(localizer[key]))
                {
                    results["visible-texts"] = texts;
                    throw new InvalidOperationException($"Language binding did not update on {page}");
                }
                results[$"{page}.live-language-change"] = true;
                localizer.ChangeLanguage("es");
                await SaveScreenshotAsync(window.VerificationRoot, Path.Combine(directory, page + ".png"));
            }
            window.ShowPage("workspace");
            VerifyOverlay(window.VerificationOverlay, directory);
            results["overlay.transparency-paging-and-focus"] = true;
            results["overlay.frosted-backdrop"] = true;
            results["success"] = true;
        }
        catch (Exception exception) { results["success"] = false; results["error"] = exception.ToString(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "ui-verification.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        window.Close();
    }

    private static void VerifyOverlay(OverlayWindow overlay, string directory)
    {
        var file = FileAsset.Create(Path.Combine(Path.GetFullPath(directory), "paisaje.png"), 2_621_440, DateTimeOffset.UtcNow);
        var foreground = NativeMethods.GetForegroundWindow();
        using var backdrop = CreateGlassBackdrop();
        try
        {
            overlay.ShowAtCursor(file, backdrop);
            if (!overlay.IsVisible || overlay.VisibleActions.Any(action => action.Group != ActionGroup.Convert))
                throw new InvalidOperationException("A supported file should start on conversions.");
            if (NativeMethods.GetForegroundWindow() != foreground)
                throw new InvalidOperationException("Showing the overlay must preserve focus.");

            using var conversions = overlay.Snapshot();
            var hubAlpha = conversions.GetPixel(conversions.Width * 3 / 8, conversions.Height / 2).A;
            if (conversions.GetPixel(0, 0).A != 0 || hubAlpha is 0 or 255)
                throw new InvalidOperationException("The wheel must use a transparent canvas with a translucent hub.");
            using var frost = RadialGlass.CreateFrost(backdrop);
            var dot = backdrop.GetPixel(13, 13);
            var blurredDot = frost.GetPixel(13, 13);
            if (blurredDot.A is < 160 or > 185 || blurredDot.R <= dot.R + 65)
                throw new InvalidOperationException($"Expected a softened, translucent dot. Original {dot}, frost {blurredDot}.");
            if (RadialGlass.SectorAt(208, 65, overlay.VisibleActions.Count) != 0
                || RadialGlass.SectorAt(208, 208, overlay.VisibleActions.Count) != -1)
                throw new InvalidOperationException("The center must not trigger an action and the first sector must be clickable.");
            conversions.Save(Path.Combine(directory, "wheel-conversions.png"), ImageFormat.Png);

            overlay.Scroll(-60);
            if (overlay.VisibleActions.Any(action => action.Group != ActionGroup.Convert))
                throw new InvalidOperationException("High-resolution wheels must accumulate a full tick before paging.");
            overlay.Scroll(-60);
            if (overlay.VisibleActions.Count != 5 || overlay.VisibleActions.Any(action => action.Group != ActionGroup.Tools))
                throw new InvalidOperationException("Scrolling must reveal the five image tools.");
            using var tools = overlay.Snapshot();
            tools.Save(Path.Combine(directory, "wheel-tools.png"), ImageFormat.Png);
            SaveGlassPreview(backdrop, tools, Path.Combine(directory, "wheel-glass-preview.png"));
            overlay.Scroll(120);
            if (overlay.VisibleActions.Any(action => action.Group != ActionGroup.Convert))
                throw new InvalidOperationException("Scrolling in the opposite direction must return to conversions.");
        }
        finally { overlay.Hide(); }
    }

    private static Drawing.Bitmap CreateGlassBackdrop()
    {
        var size = RadialGlass.Size;
        var bitmap = new Drawing.Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Drawing.Rectangle(0, 0, size, size);
        using var background = new DrawingGradient(bounds, Drawing.Color.FromArgb(236, 237, 241),
            Drawing.Color.FromArgb(192, 200, 214), 52f);
        graphics.FillRectangle(background, bounds);
        using var dots = new Drawing.SolidBrush(Drawing.Color.FromArgb(115, 123, 140));
        for (var y = 12; y < size; y += 16)
        for (var x = 12; x < size; x += 16)
            graphics.FillRectangle(dots, x, y, 2, 2);

        using var upper = new DrawingGradient(bounds, Drawing.Color.FromArgb(134, 150, 177),
            Drawing.Color.FromArgb(96, 109, 138), 90f);
        graphics.FillEllipse(upper, 241, -40, 148, 186);
        using var lower = new DrawingGradient(bounds, Drawing.Color.FromArgb(244, 243, 247),
            Drawing.Color.FromArgb(154, 169, 191), 32f);
        graphics.FillEllipse(lower, -59, 244, 193, 173);
        return bitmap;
    }

    private static void SaveGlassPreview(Drawing.Bitmap backdrop, Drawing.Bitmap wheel, string path)
    {
        using var preview = new Drawing.Bitmap(wheel.Width, wheel.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Drawing.Graphics.FromImage(preview))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(backdrop, 0, 0, wheel.Width, wheel.Height);
            graphics.DrawImageUnscaled(wheel, 0, 0);
        }
        preview.Save(path, ImageFormat.Png);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static async Task SaveScreenshotAsync(FrameworkElement root, string path)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(root);
        var pixels = await bitmap.GetPixelsAsync();
        await using var stream = File.Create(path);
        using var random = stream.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, random);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
