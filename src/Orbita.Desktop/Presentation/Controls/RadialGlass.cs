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

    public static Bitmap CreateFrost(Bitmap source)
    {
        const int blurSamples = 28;
        using var reduced = new Bitmap(blurSamples, blurSamples, PixelFormat.Format24bppRgb);
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        using (var downsample = Graphics.FromImage(reduced))
        {
            downsample.InterpolationMode = InterpolationMode.HighQualityBicubic;
            downsample.DrawImage(source, new Rectangle(0, 0, blurSamples, blurSamples),
                0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }

        var frost = new Bitmap(Size, Size, PixelFormat.Format32bppPArgb);
        try
        {
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = .68f });
            using var upsample = Graphics.FromImage(frost);
            upsample.InterpolationMode = InterpolationMode.HighQualityBicubic;
            upsample.DrawImage(reduced, new Rectangle(0, 0, Size, Size),
                0, 0, reduced.Width, reduced.Height, GraphicsUnit.Pixel, attributes);
            return frost;
        }
        catch { frost.Dispose(); throw; }
    }

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
        ILocalizer text, int page, int pageCount, int highlight, bool light, Bitmap? backdrop)
    {
        var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.ScaleTransform((float)pixels / Size, (float)pixels / Size);
            using var frost = backdrop is null ? null : new TextureBrush(backdrop, WrapMode.Clamp);
            var ink = Color.FromArgb(23, 29, 39);
            var accent = Color.FromArgb(62, 73, 89);
            var step = 360f / actions.Length;
            for (var index = 0; index < actions.Length; index++)
            {
                var angle = -90 + index * step;
                using var shape = CreateSector(angle, Sweep(step));
                var active = index == highlight;
                PaintGlass(graphics, shape, frost, light, active);
                var middle = Polar(143, angle);
                var action = actions[index];
                Label(graphics, action.Glyph, middle.X, middle.Y - 13, 22, 44,
                    active ? Color.FromArgb(34, 72, 133) : accent, "Segoe MDL2 Assets");
                Label(graphics, text[action.LabelKey], middle.X, middle.Y + 14, 13, 114, ink, style: FontStyle.Bold);
            }

            using var hub = new GraphicsPath();
            hub.AddEllipse(Circle(HubRadius));
            PaintGlass(graphics, hub, frost, light, false);
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

    private static GraphicsPath CreateSector(float angle, float sweep)
    {
        const float corner = 11;
        const float outerTrim = corner / OuterRadius * (180 / MathF.PI);
        const float innerTrim = corner / InnerRadius * (180 / MathF.PI);
        var start = angle - sweep / 2;
        var end = angle + sweep / 2;
        var shape = new GraphicsPath();
        shape.AddArc(Circle(OuterRadius), start + outerTrim, sweep - 2 * outerTrim);
        AddCorner(shape, Polar(OuterRadius, end - outerTrim), Polar(OuterRadius, end), Polar(OuterRadius - corner, end));
        shape.AddLine(Polar(OuterRadius - corner, end), Polar(InnerRadius + corner, end));
        AddCorner(shape, Polar(InnerRadius + corner, end), Polar(InnerRadius, end), Polar(InnerRadius, end - innerTrim));
        shape.AddArc(Circle(InnerRadius), end - innerTrim, -sweep + 2 * innerTrim);
        AddCorner(shape, Polar(InnerRadius, start + innerTrim), Polar(InnerRadius, start), Polar(InnerRadius + corner, start));
        shape.AddLine(Polar(InnerRadius + corner, start), Polar(OuterRadius - corner, start));
        AddCorner(shape, Polar(OuterRadius - corner, start), Polar(OuterRadius, start), Polar(OuterRadius, start + outerTrim));
        shape.CloseFigure();
        return shape;
    }

    private static void AddCorner(GraphicsPath shape, PointF from, PointF corner, PointF to)
    {
        static PointF Control(PointF endpoint, PointF vertex) =>
            new((endpoint.X + 2 * vertex.X) / 3, (endpoint.Y + 2 * vertex.Y) / 3);
        shape.AddBezier(from, Control(from, corner), Control(to, corner), to);
    }

    private static void PaintGlass(Graphics graphics, GraphicsPath shape, TextureBrush? frost, bool light, bool active)
    {
        var state = graphics.Save();
        graphics.TranslateTransform(0, 2);
        using (var shadow = new Pen(Color.FromArgb(13, 30, 35, 43)) { LineJoin = LineJoin.Round })
        {
            for (var width = 12; width >= 2; width -= 2)
            {
                shadow.Width = width;
                graphics.DrawPath(shadow, shape);
            }
        }
        graphics.Restore(state);

        var bounds = shape.GetBounds();
        using var bevel = new LinearGradientBrush(bounds, Color.FromArgb(147, 255, 255, 255),
            Color.FromArgb(52, 40, 52, 67), 65f);
        using var rim = new Pen(bevel, 4.5f);
        graphics.DrawPath(rim, shape);

        if (frost is not null) graphics.FillPath(frost, shape);
        var tint = active ? Color.FromArgb(232, 241, 255)
            : light ? Color.FromArgb(244, 247, 251) : Color.FromArgb(228, 234, 242);
        using var milk = new LinearGradientBrush(bounds, Color.White, tint, 65f)
        {
            InterpolationColors = new ColorBlend
            {
                Positions = [0, .19f, .48f, .77f, 1],
                Colors = [Color.FromArgb(225, Color.White), Color.FromArgb(164, Color.White),
                    Color.FromArgb(frost is null ? 185 : 133, tint), Color.FromArgb(151, tint),
                    Color.FromArgb(205, Color.White)]
            }
        };
        graphics.FillPath(milk, shape);

        using var reflection = new LinearGradientBrush(bounds, Color.White, tint, 65f)
        {
            InterpolationColors = new ColorBlend
            {
                Positions = [0, .28f, .61f, .85f, 1],
                Colors = [Color.FromArgb(252, Color.White), Color.FromArgb(220, Color.White),
                    Color.FromArgb(86, 50, 63, 81), Color.FromArgb(145, 46, 58, 74),
                    Color.FromArgb(242, Color.White)]
            }
        };
        using var edge = new Pen(reflection, active ? 2.15f : 1.25f);
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
