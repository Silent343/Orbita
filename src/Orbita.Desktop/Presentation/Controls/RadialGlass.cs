using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Orbita.Files.Domain;
using Orbita.Localization;

namespace Orbita.Desktop.Presentation.Controls;

internal static class RadialGlass
{
    public const int Size = 416;
    private const float Center = Size / 2f;
    private const float HubRadius = 78;
    private const float InnerRadius = 90;
    private const float OuterRadius = 194;
    private const float GapDegrees = 5;

    public static int SectorAt(float x, float y, int count)
    {
        if (count == 0) return -1;
        x -= Center;
        y -= Center;
        var distance = x * x + y * y;
        if (distance < InnerRadius * InnerRadius || distance > OuterRadius * OuterRadius) return -1;
        var step = 360f / count;
        var angle = (Math.Atan2(y, x) * 180 / Math.PI + 450 + step / 2) % 360;
        var offset = angle % step;
        return Math.Abs(offset - step / 2) > Sweep(step) / 2 ? -1 : (int)(angle / step);
    }

    public static Bitmap Render(int pixels, FileAsset file, string fileSize, FileAction[] actions,
        ILocalizer text, int page, int pageCount, int highlight, bool light)
    {
        var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.ScaleTransform((float)pixels / Size, (float)pixels / Size);
            var ink = light ? Color.FromArgb(14, 34, 38) : Color.FromArgb(246, 253, 252);
            var accent = light ? Color.FromArgb(17, 85, 74) : Color.FromArgb(169, 239, 218);
            var step = 360f / actions.Length;
            for (var index = 0; index < actions.Length; index++)
            {
                var angle = -90 + index * step;
                using var shape = new GraphicsPath();
                var sweep = Sweep(step);
                shape.AddArc(Circle(OuterRadius), angle - sweep / 2, sweep);
                shape.AddArc(Circle(InnerRadius), angle + sweep / 2, -sweep);
                shape.CloseFigure();
                PaintGlass(graphics, shape, light, index == highlight);
                var middle = Polar(143, angle);
                var action = actions[index];
                Label(graphics, action.Glyph, middle.X, middle.Y - 13, 22, 44, accent, "Segoe MDL2 Assets");
                Label(graphics, text[action.LabelKey], middle.X, middle.Y + 14, 13, 114, ink, style: FontStyle.Bold);
            }

            using var hub = new GraphicsPath();
            hub.AddEllipse(Circle(HubRadius));
            PaintGlass(graphics, hub, light, false);
            DrawFileIcon(graphics, ink, accent, file.Extension.TrimStart('.').ToUpperInvariant());
            Label(graphics, fileSize, Center, Center + 8, 23, 138, ink, style: FontStyle.Bold);
            Label(graphics, file.Name, Center, Center + 34, 11, 126, ink);
            if (pageCount > 1)
            {
                using var dot = new SolidBrush(accent);
                for (var index = 0; index < pageCount; index++)
                {
                    dot.Color = Color.FromArgb(index == page ? 255 : 115, accent);
                    var x = Center + (index - (pageCount - 1) / 2f) * 9;
                    graphics.FillEllipse(dot, x - 2, Center + 51, 4, 4);
                }
            }
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    private static void PaintGlass(Graphics graphics, GraphicsPath shape, bool light, bool active)
    {
        var top = light ? Color.FromArgb(234, 242, 254, 252) : Color.FromArgb(225, 53, 80, 85);
        var bottom = light ? Color.FromArgb(217, 178, 214, 210) : Color.FromArgb(226, 16, 35, 43);
        if (active)
        {
            top = light ? Color.FromArgb(248, 220, 255, 239) : Color.FromArgb(250, 51, 106, 97);
            bottom = light ? Color.FromArgb(243, 157, 225, 205) : Color.FromArgb(248, 21, 66, 69);
        }
        using var fill = new LinearGradientBrush(shape.GetBounds(), top, bottom, 72f);
        using var edge = new Pen(Color.FromArgb(active ? 235 : 145, 219, 255, 245), active ? 1.8f : 1f);
        graphics.FillPath(fill, shape);
        graphics.DrawPath(edge, shape);
    }

    private static void DrawFileIcon(Graphics graphics, Color ink, Color accent, string extension)
    {
        using var pen = new Pen(accent, 1.35f) { LineJoin = LineJoin.Round };
        using var shape = new GraphicsPath();
        shape.AddPolygon(new PointF[] {
            new(Center - 15, Center - 54), new(Center + 3, Center - 54), new(Center + 15, Center - 42),
            new(Center + 15, Center - 18), new(Center - 15, Center - 18) });
        graphics.DrawPath(pen, shape);
        graphics.DrawLines(pen, [new PointF(Center + 3, Center - 54), new(Center + 3, Center - 42), new(Center + 15, Center - 42)]);
        Label(graphics, extension, Center, Center - 29, 8, 27, ink, style: FontStyle.Bold);
    }

    private static void Label(Graphics graphics, string value, float x, float y, float size, float width,
        Color color, string family = "Segoe UI", FontStyle style = FontStyle.Regular)
    {
        using var font = new Font(family, size, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        graphics.DrawString(value, font, brush, new RectangleF(x - width / 2, y - size, width, 2 * size), format);
    }

    private static RectangleF Circle(float radius) => new(Center - radius, Center - radius, 2 * radius, 2 * radius);
    private static float Sweep(float step) => Math.Min(87, step - GapDegrees);

    private static PointF Polar(float radius, float angle)
    {
        var radians = angle * Math.PI / 180;
        return new PointF(Center + radius * (float)Math.Cos(radians), Center + radius * (float)Math.Sin(radians));
    }
}
