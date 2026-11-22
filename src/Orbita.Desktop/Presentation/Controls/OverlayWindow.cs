using System.Drawing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Orbita.Desktop.WindowsIntegration;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.Localization;
using Windows.Graphics;

namespace Orbita.Desktop.Presentation.Controls;

internal sealed class OverlayWindow : IDisposable
{
    private readonly LayeredPopup _popup = new();
    private readonly IActionCatalog _catalog;
    private readonly ILocalizer _localizer;
    private readonly DispatcherQueueTimer _pointerTimer;
    private readonly Action<Exception> _onError;
    private FileAction[][] _pages = [];
    private FileAsset? _file;
    private Bitmap? _frost;
    private Rectangle _bounds;
    private int _page;
    private int _highlight = -1;
    private int _pressed = -1;
    private int _scrollDelta;
    private bool _light;
    internal bool IsVisible => _file is not null;
    internal IReadOnlyList<FileAction> VisibleActions => _pages[_page];
    public event EventHandler<FileAction>? ActionRequested;

    public OverlayWindow(IActionCatalog catalog, ILocalizer localizer, DispatcherQueue dispatcher, Action<Exception> onError)
    {
        (_catalog, _localizer, _onError) = (catalog, localizer, onError);
        _popup.Scrolled += delta => _scrollDelta += delta;
        _popup.Pressed += (x, y) => _pressed = HitTest(x, y);
        _popup.Released += (x, y) =>
        {
            var hit = HitTest(x, y);
            var pressed = _pressed;
            _pressed = -1;
            if (hit < 0 || hit != pressed) return;
            var action = _pages[_page][hit];
            Hide();
            dispatcher.TryEnqueue(() => ActionRequested?.Invoke(this, action));
        };
        _pointerTimer = dispatcher.CreateTimer();
        _pointerTimer.Interval = TimeSpan.FromMilliseconds(24);
        _pointerTimer.Tick += (_, _) => UpdatePointer();
    }

    public void ApplyTheme(bool light)
    {
        _light = light;
        if (IsVisible) Draw();
    }

    public void ShowAtCursor(FileAsset file) => ShowAtCursor(file, null);

    internal void ShowAtCursor(FileAsset file, Bitmap? testBackdrop)
    {
        if (IsVisible) Hide();
        _pages = Enum.GetValues<ActionGroup>()
            .SelectMany(group => _catalog.For(file, group).Where(action => action.Available).Chunk(5))
            .ToArray();
        if (_pages.Length == 0 || !NativeMethods.GetCursorPos(out var point)) return;

        var area = DisplayArea.GetFromPoint(new PointInt32(point.X, point.Y), DisplayAreaFallback.Primary).WorkArea;
        var dpi = NativeMethods.GetDpiForWindow(NativeMethods.GetForegroundWindow());
        var pixels = Math.Min((int)Math.Round(RadialGlass.Size * (dpi == 0 ? 1 : dpi / 96.0)),
            Math.Min(area.Width, area.Height));
        var x = Math.Clamp(point.X - pixels / 2, area.X, area.X + area.Width - pixels);
        var y = Math.Clamp(point.Y - pixels / 2, area.Y, area.Y + area.Height - pixels);
        _bounds = new Rectangle(x, y, pixels, pixels);
        _file = file;
        _page = 0;
        _highlight = _pressed = -1;
        _scrollDelta = 0;
        try
        {
            // Keep one capture in memory while the wheel is open, including during hover and paging.
            using var captured = testBackdrop is null ? ScreenBackdrop.Capture(_bounds) : null;
            _frost = (testBackdrop ?? captured) is { } backdrop ? RadialGlass.CreateFrost(backdrop) : null;
            Draw();
            _pointerTimer.Start();
        }
        catch
        {
            Hide();
            throw;
        }
    }

    private int HitTest(int x, int y)
    {
        var scale = (float)RadialGlass.Size / _bounds.Width;
        return RadialGlass.SectorAt(x * scale, y * scale, _pages[_page].Length);
    }

    private void UpdatePointer()
    {
        if (!IsVisible || !NativeMethods.GetCursorPos(out var point)) return;
        var previous = _page;
        var ticks = _scrollDelta / 120;
        if (ticks != 0)
        {
            _scrollDelta %= 120;
            _page = ((_page - ticks) % _pages.Length + _pages.Length) % _pages.Length;
            if (_page != previous) _pressed = -1;
        }
        var next = HitTest(point.X - _bounds.X, point.Y - _bounds.Y);
        if (next == _highlight && _page == previous) return;
        _highlight = next;
        try { Draw(); }
        catch (Exception error) { Hide(); _onError(error); }
    }

    private void Draw()
    {
        if (_file is null) return;
        using var bitmap = Snapshot();
        _popup.Present(bitmap, _bounds);
    }

    internal Bitmap Snapshot() => RadialGlass.Render(_bounds.Width, _file!,
        FileSizeFormatter.Format(_file!.Size, _localizer.Culture),
        _pages[_page], _localizer, _page, _pages.Length, _highlight, _light, _frost);

    internal void Scroll(int delta) { _scrollDelta += delta; UpdatePointer(); }

    public void Hide()
    {
        _pointerTimer.Stop();
        _file = null;
        _popup.Hide();
        _frost?.Dispose();
        _frost = null;
    }

    public void Dispose()
    {
        Hide();
        _popup.Dispose();
    }
}
