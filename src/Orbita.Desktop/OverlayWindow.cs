using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Orbita.Desktop.Presentation;
using Orbita.Desktop.Presentation.Controls;
using Orbita.Desktop.WindowsIntegration;
using Orbita.Files.Domain;
using Orbita.Localization;
using Windows.Graphics;

namespace Orbita.Desktop;

public sealed class OverlayWindow : Window
{
    private readonly Grid _root;
    private readonly RadialWheel _wheel;
    private readonly ILocalizer _localizer;
    public event EventHandler<FileAction>? ActionRequested;
    public event EventHandler? OpenApplicationRequested;
    public bool IsVisible { get; private set; }

    public OverlayWindow(WorkspaceViewModel workspace, ILocalizer localizer)
    {
        _localizer = localizer;
        Title = localizer["OverlayTitle"];
        SystemBackdrop = new DesktopAcrylicBackdrop();
        _root = new Grid { Padding = new Thickness(16), RowSpacing = 4 };
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = Ui.Text(localizer, "AppName", 16);
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        var close = new Button { Content = new FontIcon { Glyph = "\uE8BB", FontSize = 12 }, Padding = new Thickness(8) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, localizer["Close"]);
        close.Click += (_, _) => Hide();
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        _root.Children.Add(header);
        _wheel = new RadialWheel(workspace);
        _wheel.ActionRequested += (_, action) => { Hide(); ActionRequested?.Invoke(this, action); };
        Grid.SetRow(_wheel, 1);
        _root.Children.Add(_wheel);
        var footer = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        var hint = Ui.Text(localizer, "OverlayHint", 11, .65);
        hint.TextAlignment = TextAlignment.Center;
        footer.Children.Add(hint);
        var open = Ui.Button(localizer, "OverlayOpenApp");
        open.HorizontalAlignment = HorizontalAlignment.Center;
        open.Click += (_, _) => { Hide(); OpenApplicationRequested?.Invoke(this, EventArgs.Empty); };
        footer.Children.Add(open);
        Grid.SetRow(footer, 2);
        _root.Children.Add(footer);
        Content = _root;
        ExtendsContentIntoTitleBar = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        AppWindow.IsShownInSwitchers = false;
        localizer.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => localizer.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs arguments) => Title = _localizer["OverlayTitle"];

    public void ApplyTheme(ElementTheme theme)
    {
        _root.RequestedTheme = theme;
        _wheel.Render();
    }

    public void ShowAtCursor()
    {
        NativeMethods.GetCursorPos(out var point);
        var area = DisplayArea.GetFromPoint(new PointInt32(point.X, point.Y), DisplayAreaFallback.Primary).WorkArea;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = Math.Max(1, NativeMethods.GetDpiForWindow(handle) / 96.0);
        var width = Math.Min((int)(620 * scale), area.Width);
        var height = Math.Min((int)(650 * scale), area.Height);
        var x = Math.Clamp(point.X - width / 2, area.X, Math.Max(area.X, area.X + area.Width - width));
        var y = Math.Clamp(point.Y - height / 2, area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        AppWindow.Show(false);
        NativeMethods.SetWindowPos(handle, new nint(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        IsVisible = true;
    }

    public void Hide() { AppWindow.Hide(); IsVisible = false; }
}
