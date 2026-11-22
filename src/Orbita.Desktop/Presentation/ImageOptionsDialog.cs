using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Orbita.Files.Domain;
using Orbita.Localization;
using Windows.Graphics.Imaging;

namespace Orbita.Desktop.Presentation;

public static class ImageOptionsDialog
{
    public static async Task<ImageEditOptions?> ShowAsync(XamlRoot root, FileAsset file, bool crop, ILocalizer localizer)
    {
        await using var stream = System.IO.File.OpenRead(file.Path);
        using var random = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(random);
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(Ui.Text(localizer, crop ? "CropDescription" : "ResizeDescription", 13, .7));
        NumberBox Number(string key, double value, double minimum, double maximum)
        {
            var box = new NumberBox { Value = value, Minimum = minimum, Maximum = maximum,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
            Ui.Bind(box, NumberBox.HeaderProperty, localizer, $"[{key}]");
            panel.Children.Add(box);
            return box;
        }
        var left = crop ? Number("Left", 0, 0, decoder.OrientedPixelWidth - 1) : null;
        var top = crop ? Number("Top", 0, 0, decoder.OrientedPixelHeight - 1) : null;
        var width = Number("Width", decoder.OrientedPixelWidth, 1, crop ? decoder.OrientedPixelWidth : 16384);
        var height = Number("Height", decoder.OrientedPixelHeight, 1, crop ? decoder.OrientedPixelHeight : 16384);
        var dialog = new ContentDialog { XamlRoot = root, Title = localizer[crop ? "CropTitle" : "DimensionsTitle"],
            Content = panel, PrimaryButtonText = localizer["Continue"], CloseButtonText = localizer["Cancel"],
            DefaultButton = ContentDialogButton.Primary };
        Ui.Bind(dialog, ContentDialog.TitleProperty, localizer, crop ? "[CropTitle]" : "[DimensionsTitle]");
        Ui.Bind(dialog, ContentDialog.PrimaryButtonTextProperty, localizer, "[Continue]");
        Ui.Bind(dialog, ContentDialog.CloseButtonTextProperty, localizer, "[Cancel]");
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        static uint Value(NumberBox? box) => box is null || !double.IsFinite(box.Value) ? 0 : (uint)Math.Max(0, box.Value);
        return crop
            ? new ImageEditOptions(Left: Value(left), Top: Value(top), CropWidth: Value(width), CropHeight: Value(height))
            : new ImageEditOptions(Width: Value(width), Height: Value(height));
    }
}
