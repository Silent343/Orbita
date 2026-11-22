using System.Runtime.InteropServices;

namespace Orbita.Desktop.WindowsIntegration;

public interface IExplorerSelectionReader
{
    string? GetSingleSelectedFile(nint foregroundWindow);
}

/// <summary>Reads Explorer selection through Shell automation; never touches the clipboard.</summary>
public sealed class ExplorerSelectionReader : IExplorerSelectionReader
{
    public string? GetSingleSelectedFile(nint foregroundWindow)
    {
        object? shell = null;
        object? windows = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return null;
            shell = Activator.CreateInstance(shellType);
            windows = ((dynamic)shell!).Windows();
            if (NativeMethods.IsDesktop(foregroundWindow))
            {
                object location = 0;
                object root = 0;
                int desktopHandle;
                object? desktop = null;
                try
                {
                    desktop = ((dynamic)windows).FindWindowSW(ref location, ref root, 8, out desktopHandle, 1);
                    return desktop is null ? null : ReadSelection(desktop);
                }
                finally { Release(desktop); }
            }
            var count = (int)((dynamic)windows).Count;
            for (var index = 0; index < count; index++)
            {
                object? window = null;
                try
                {
                    window = ((dynamic)windows).Item(index);
                    if (window is not null && (long)((dynamic)window).HWND == foregroundWindow.ToInt64())
                        return ReadSelection(window);
                }
                finally { Release(window); }
            }
            return null;
        }
        catch (COMException) { return null; }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return null; }
        finally { Release(windows); Release(shell); }
    }

    private static string? ReadSelection(object window)
    {
        object? document = null;
        object? selected = null;
        object? item = null;
        try
        {
            document = ((dynamic)window).Document;
            selected = ((dynamic)document).SelectedItems();
            if ((int)((dynamic)selected).Count != 1) return null;
            item = ((dynamic)selected).Item(0);
            if ((bool)((dynamic)item).IsFolder) return null;
            var path = (string)((dynamic)item).Path;
            return File.Exists(path) ? path : null;
        }
        finally { Release(item); Release(selected); Release(document); }
    }

    private static void Release(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
    }
}
