using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Orbita.Desktop.WindowsIntegration;

// WinUI hosts preferences. This window supplies per-pixel alpha and leaves the Explorer focused.
internal sealed class LayeredPopup : IDisposable
{
    private const uint MouseWheel = 0x020A;
    private const uint LeftButtonDown = 0x0201;
    private const uint LeftButtonUp = 0x0202;
    private readonly WindowProcedure _procedure;
    private readonly HookProcedure _mouseProcedure;
    private readonly nint _module = GetModuleHandle(null);
    private readonly string _className = "Orbita.Radial." + Guid.NewGuid().ToString("N");
    private readonly nint _handle;
    private nint _mouseHook;
    private Rectangle _bounds;
    private bool _visible;
    private bool _disposed;

    public event Action<int>? Scrolled;
    public event Action<int, int>? Pressed;
    public event Action<int, int>? Released;

    public LayeredPopup()
    {
        _procedure = ProcessMessage;
        _mouseProcedure = ProcessMouse;
        var windowClass = new WindowClass
        {
            Instance = _module, Name = _className,
            Procedure = Marshal.GetFunctionPointerForDelegate(_procedure),
            Cursor = LoadCursor(0, new nint(32512))
        };
        if (RegisterClass(ref windowClass) == 0) throw new Win32Exception();
        const uint styles = 0x80000 | 0x08000000 | 0x80; // Layered, no activate, tool window
        _handle = CreateWindowEx(styles, _className, "Órbita", 0x80000000, 0, 0, 0, 0, 0, 0, _module, 0);
        if (_handle != 0) return;
        var error = new Win32Exception();
        UnregisterClass(_className, _module);
        throw error;
    }

    public void Present(Bitmap bitmap, Rectangle bounds)
    {
        var dc = CreateCompatibleDC(0);
        if (dc == 0) throw new Win32Exception();
        nint dib = 0;
        nint previous = 0;
        try
        {
            var size = new NativeMethods.Point { X = bitmap.Width, Y = bitmap.Height };
            var header = new BitmapHeader { Size = 40, Width = size.X, Height = -size.Y, Planes = 1, BitCount = 32 };
            dib = CreateDIBSection(dc, ref header, 0, out var bits, 0, 0);
            if (dib == 0 || bits == 0) throw new Win32Exception();
            previous = SelectObject(dc, dib);
            if (previous == 0 || previous == new nint(-1)) throw new Win32Exception();
            CopyPixels(bitmap, bits);
            var position = new NativeMethods.Point { X = bounds.X, Y = bounds.Y };
            var source = new NativeMethods.Point();
            var blend = new Blend { Opacity = 255, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(_handle, 0, ref position, ref size, dc, ref source, 0, ref blend, 2))
                throw new Win32Exception();
            _bounds = bounds;
            if (_visible) return;
            _mouseHook = SetWindowsHookEx(14, _mouseProcedure, _module, 0);
            if (_mouseHook == 0) throw new Win32Exception();
            _visible = NativeMethods.SetWindowPos(_handle, new nint(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
            if (!_visible) { Hide(); throw new Win32Exception(); }
        }
        finally
        {
            if (previous != 0 && previous != new nint(-1)) SelectObject(dc, previous);
            if (dib != 0) DeleteObject(dib);
            DeleteDC(dc);
        }
    }

    private static void CopyPixels(Bitmap bitmap, nint destination)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var row = new byte[bitmap.Width * 4];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                Marshal.Copy(row, 0, destination + y * row.Length, row.Length);
            }
        }
        finally { bitmap.UnlockBits(data); }
    }

    public void Hide()
    {
        _visible = false;
        ShowWindow(_handle, 0);
        if (_mouseHook == 0) return;
        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = 0;
    }

    private nint ProcessMessage(nint window, uint message, nint wParam, nint lParam)
    {
        if (message == 0x0021) return 3; // MA_NOACTIVATE
        if (_visible && message is LeftButtonDown or LeftButtonUp)
        {
            var x = (short)(lParam.ToInt64() & 0xffff);
            var y = (short)(lParam.ToInt64() >> 16);
            (message == LeftButtonDown ? Pressed : Released)?.Invoke(x, y);
            return 0;
        }
        return DefWindowProc(window, message, wParam, lParam);
    }

    private nint ProcessMouse(int code, nint message, nint data)
    {
        if (code >= 0 && _visible && message == MouseWheel)
        {
            var mouse = Marshal.PtrToStructure<MouseData>(data);
            if (_bounds.Contains(mouse.Point.X, mouse.Point.Y))
            {
                Scrolled?.Invoke((short)(mouse.Data >> 16));
                return 1;
            }
        }
        return CallNextHookEx(0, code, message, data);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Hide();
        DestroyWindow(_handle);
        UnregisterClass(_className, _module);
    }

    private delegate nint WindowProcedure(nint window, uint message, nint wParam, nint lParam);
    private delegate nint HookProcedure(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? Menu, Name;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, ImageSize;
        public int XPixelsPerMeter, YPixelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Blend { public byte Operation, Flags, Opacity, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData { public NativeMethods.Point Point; public uint Data, Flags, Time; public nint Extra; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClass(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadCursor(nint instance, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extended, string className, string title, uint style, int x, int y,
        int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int type, HookProcedure callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(nint window, nint destination, ref NativeMethods.Point position,
        ref NativeMethods.Point size, nint source, ref NativeMethods.Point sourcePoint, uint color, ref Blend blend, uint flags);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapHeader info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
}
