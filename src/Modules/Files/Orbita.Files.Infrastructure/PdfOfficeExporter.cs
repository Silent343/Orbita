using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Orbita.SharedKernel;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Orbita.Files.Infrastructure;

internal static class PdfOfficeExporter
{
    public static async Task WriteWordAsync(string source, Stream output, CancellationToken cancellationToken)
    {
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(source);
        using var ocr = new PdfPageOcr(source);
        using var document = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);
        using var writer = OpenXmlWriter.Create(document.AddMainDocumentPart());
        writer.WriteStartElement(new W.Document());
        writer.WriteStartElement(new W.Body());
        var foundText = false;
        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (page.Number > 1)
                writer.WriteElement(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
            var extracted = ContentOrderTextExtractor.GetText(page);
            var extractedCount = extracted.Count(char.IsLetterOrDigit);
            if (string.IsNullOrWhiteSpace(extracted) || (extractedCount < 24 && page.GetImages().Any()))
            {
                var recognized = await ocr.RecognizeAsync(page.Number, cancellationToken);
                if (recognized.Count(char.IsLetterOrDigit) > extractedCount) extracted = recognized;
            }
            foreach (var line in extracted.ReplaceLineEndings("\n").Split('\n'))
            {
                var text = ValidXmlText(line);
                foundText |= !string.IsNullOrWhiteSpace(text);
                writer.WriteElement(new W.Paragraph(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve })));
            }
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        if (!foundText) throw new DomainException("ErrorPdfNoEditableText");
    }

    private static string ValidXmlText(string line)
    {
        var text = new StringBuilder(line.Length);
        foreach (var rune in line.EnumerateRunes())
            if (rune.Value is 9 or >= 32 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000)
                text.Append(rune.ToString());
        return text.ToString();
    }

    public static async Task WriteSlidesAsync(PdfDocument pdf, Stream output, CancellationToken cancellationToken)
    {
        using var document = PresentationDocument.Create(output, PresentationDocumentType.Presentation);
        using var firstPage = pdf.GetPage(0);
        var firstSize = firstPage.Size;
        const int longestSlideSide = 9_144_000; // Ten inches in Office drawing units.
        var unit = longestSlideSide / Math.Max(firstSize.Width, firstSize.Height);
        var slideWidth = (int)Math.Clamp(firstSize.Width * unit, 91_440, longestSlideSide);
        var slideHeight = (int)Math.Clamp(firstSize.Height * unit, 91_440, longestSlideSide);
        var presentation = document.AddPresentationPart();
        var master = presentation.AddNewPart<SlideMasterPart>();
        var layout = master.AddNewPart<SlideLayoutPart>();
        layout.AddPart(master);
        layout.SlideLayout = new P.SlideLayout(new P.CommonSlideData(EmptyShapeTree()), ColorMapOverride())
            { Type = P.SlideLayoutValues.Blank, Preserve = true };
        master.SlideMaster = new P.SlideMaster(new P.CommonSlideData(EmptyShapeTree()), new P.ColorMap
        {
            Background1 = A.ColorSchemeIndexValues.Light1, Text1 = A.ColorSchemeIndexValues.Dark1,
            Background2 = A.ColorSchemeIndexValues.Light2, Text2 = A.ColorSchemeIndexValues.Dark2,
            Accent1 = A.ColorSchemeIndexValues.Accent1, Accent2 = A.ColorSchemeIndexValues.Accent2,
            Accent3 = A.ColorSchemeIndexValues.Accent3, Accent4 = A.ColorSchemeIndexValues.Accent4,
            Accent5 = A.ColorSchemeIndexValues.Accent5, Accent6 = A.ColorSchemeIndexValues.Accent6,
            Hyperlink = A.ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
        }, new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2_147_483_649U, RelationshipId = master.GetIdOfPart(layout) }));
        master.AddNewPart<ThemePart>().Theme = CreateTheme();
        var slideIds = new P.SlideIdList();
        presentation.Presentation = new P.Presentation(
            new P.SlideMasterIdList(new P.SlideMasterId { Id = 2_147_483_648U, RelationshipId = presentation.GetIdOfPart(master) }),
            slideIds, new P.SlideSize { Cx = slideWidth, Cy = slideHeight },
            new P.NotesSize { Cx = 6_858_000L, Cy = 9_144_000L });

        for (uint index = 0; index < pdf.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = pdf.GetPage(index);
            using var rendered = new InMemoryRandomAccessStream();
            await PdfActionHandler.RenderPageAsync(page, rendered, cancellationToken);
            using var image = rendered.AsStreamForRead();
            var slide = presentation.AddNewPart<SlidePart>();
            slide.AddPart(layout);
            var imagePart = slide.AddImagePart(ImagePartType.Png);
            imagePart.FeedData(image);
            var fit = Math.Min(slideWidth / page.Size.Width, slideHeight / page.Size.Height);
            var width = Math.Max(1, (long)(page.Size.Width * fit));
            var height = Math.Max(1, (long)(page.Size.Height * fit));
            var shapes = EmptyShapeTree();
            shapes.Append(new P.Picture(
                new P.NonVisualPictureProperties(
                    new P.NonVisualDrawingProperties { Id = 2U, Name = $"Page {index + 1}" },
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(new A.Blip { Embed = slide.GetIdOfPart(imagePart) }, new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = (slideWidth - width) / 2, Y = (slideHeight - height) / 2 },
                        new A.Extents { Cx = width, Cy = height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })));
            slide.Slide = new P.Slide(new P.CommonSlideData(shapes), ColorMapOverride());
            slideIds.Append(new P.SlideId { Id = 256 + index, RelationshipId = presentation.GetIdOfPart(slide) });
        }
    }

    private static P.ColorMapOverride ColorMapOverride() => new(new A.MasterColorMapping());

    private static P.ShapeTree EmptyShapeTree() => new(
        new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
            new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
        new P.GroupShapeProperties());

    private static A.Theme CreateTheme()
    {
        const string fill = "<a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill>";
        var lineStyles = string.Concat(new[] { 6350, 12700, 19050 }.Select(width => $"<a:ln w=\"{width}\">{fill}</a:ln>"));
        const string effect = "<a:effectStyle><a:effectLst/></a:effectStyle>";
        return new A.Theme($"""
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Orbita">
              <a:themeElements>
                <a:clrScheme name="Orbita">
                  <a:dk1><a:srgbClr val="000000"/></a:dk1><a:lt1><a:srgbClr val="FFFFFF"/></a:lt1>
                  <a:dk2><a:srgbClr val="243047"/></a:dk2><a:lt2><a:srgbClr val="F2F4F8"/></a:lt2>
                  <a:accent1><a:srgbClr val="3366CC"/></a:accent1><a:accent2><a:srgbClr val="BD433E"/></a:accent2>
                  <a:accent3><a:srgbClr val="508345"/></a:accent3><a:accent4><a:srgbClr val="76539A"/></a:accent4>
                  <a:accent5><a:srgbClr val="287C93"/></a:accent5><a:accent6><a:srgbClr val="CD731C"/></a:accent6>
                  <a:hlink><a:srgbClr val="0055AA"/></a:hlink><a:folHlink><a:srgbClr val="774488"/></a:folHlink>
                </a:clrScheme>
                <a:fontScheme name="Orbita">
                  <a:majorFont><a:latin typeface="Arial"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
                  <a:minorFont><a:latin typeface="Arial"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
                </a:fontScheme>
                <a:fmtScheme name="Orbita">
                  <a:fillStyleLst>{fill}{fill}{fill}</a:fillStyleLst>
                  <a:lnStyleLst>{lineStyles}</a:lnStyleLst>
                  <a:effectStyleLst>{effect}{effect}{effect}</a:effectStyleLst>
                  <a:bgFillStyleLst>{fill}{fill}{fill}</a:bgFillStyleLst>
                </a:fmtScheme>
              </a:themeElements>
            </a:theme>
            """);
    }
}
