using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Orbita.Localization;

namespace Orbita.Desktop.Presentation;

internal static class Ui
{
    public static string Theme { get; set; } = "Dark";
    public static TextBlock Text(ILocalizer localizer, string key, double size = 14, double opacity = 1)
    {
        var text = new TextBlock { FontSize = size, Opacity = opacity, TextWrapping = TextWrapping.Wrap };
        Bind(text, TextBlock.TextProperty, localizer, $"[{key}]");
        return text;
    }

    public static void Bind(DependencyObject target, DependencyProperty property, object source, string path)
    {
        if (source is ILocalizer localizer && path.StartsWith('[') && path.EndsWith(']'))
        {
            source = localizer.Strings[path[1..^1]];
            path = nameof(LocalizedString.Value);
        }
        BindingOperations.SetBinding(target, property, new Binding { Source = source, Path = new PropertyPath(path), Mode = BindingMode.OneWay });
    }

    public static Button Button(ILocalizer localizer, string key, string? glyph = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (glyph is not null) content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
        content.Children.Add(Text(localizer, key));
        var button = new Button { Content = content };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, localizer[key]);
        return button;
    }

    public static Border Card(UIElement child) => new()
    {
        Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["GlassCardStyle"], Child = child
    };

    public static StackPanel Heading(ILocalizer localizer, string title, string description)
    {
        var heading = new StackPanel { Spacing = 10 };
        var titleText = Text(localizer, title, 30);
        titleText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        heading.Children.Add(titleText);
        heading.Children.Add(Text(localizer, description, 14, .65));
        return heading;
    }

    public static Brush ResourceBrush(string key) =>
        (Brush)((ResourceDictionary)Microsoft.UI.Xaml.Application.Current.Resources.ThemeDictionaries[Theme])[key];
}
