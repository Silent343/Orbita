using System.Runtime.InteropServices;
using System.Text;

namespace Orbita.Desktop.WindowsIntegration;

internal static class NativeMethods
{
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int count);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);

    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X; public int Y; }

    internal static bool IsFileSurface(nint window)
    {
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        return name.ToString() is "CabinetWClass" or "ExploreWClass" or "Progman" or "WorkerW";
    }

    internal static bool IsDesktop(nint window)
    {
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        return name.ToString() is "Progman" or "WorkerW";
    }
}
