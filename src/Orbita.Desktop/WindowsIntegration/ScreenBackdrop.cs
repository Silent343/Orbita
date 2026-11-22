using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;

namespace Orbita.Desktop.WindowsIntegration;

internal static class ScreenBackdrop
{
    public static Bitmap? Capture(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            return bitmap;
        }
        catch (Win32Exception)
        {
            bitmap.Dispose();
            return null;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
