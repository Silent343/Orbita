using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
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
        try
        {
            overlay.ShowAtCursor(file);
            if (!overlay.IsVisible || overlay.VisibleActions.Any(action => action.Group != ActionGroup.Convert))
                throw new InvalidOperationException("A supported file should start on conversions.");
            if (NativeMethods.GetForegroundWindow() != foreground)
                throw new InvalidOperationException("Showing the overlay must preserve focus.");

            using var conversions = overlay.Snapshot();
            if (conversions.GetPixel(0, 0).A != 0 || conversions.GetPixel(conversions.Width / 2, conversions.Height / 2).A == 0)
                throw new InvalidOperationException("The wheel must use a transparent canvas with a visible hub.");
            if (RadialGlass.SectorAt(208, 65, overlay.VisibleActions.Count) != 0
                || RadialGlass.SectorAt(208, 208, overlay.VisibleActions.Count) != -1)
                throw new InvalidOperationException("The center must not trigger an action and the first sector must be clickable.");
            conversions.Save(Path.Combine(directory, "wheel-conversions.png"), System.Drawing.Imaging.ImageFormat.Png);

            overlay.Scroll(-60);
            if (overlay.VisibleActions.Any(action => action.Group != ActionGroup.Convert))
                throw new InvalidOperationException("High-resolution wheels must accumulate a full tick before paging.");
            overlay.Scroll(-60);
            if (overlay.VisibleActions.Count != 5 || overlay.VisibleActions.Any(action => action.Group != ActionGroup.Tools))
                throw new InvalidOperationException("Scrolling must reveal the five image tools.");
            using var tools = overlay.Snapshot();
            tools.Save(Path.Combine(directory, "wheel-tools.png"), System.Drawing.Imaging.ImageFormat.Png);
            overlay.Scroll(120);
            if (overlay.VisibleActions.Any(action => action.Group != ActionGroup.Convert))
                throw new InvalidOperationException("Scrolling in the opposite direction must return to conversions.");
        }
        finally { overlay.Hide(); }
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
