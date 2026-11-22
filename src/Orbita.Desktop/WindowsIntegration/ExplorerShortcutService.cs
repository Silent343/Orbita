using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Orbita.Desktop.Services;

namespace Orbita.Desktop.WindowsIntegration;

public sealed class ExplorerShortcutService(PreferencesState preferences, IExplorerSelectionReader selection) : IDisposable
{
    private DispatcherQueueTimer? _timer;
    private readonly Stopwatch _hold = new();
    private bool _triggered;
    private bool _dismissed;
    private nint _sourceWindow;
    public event EventHandler<string>? FileRequested;
    public event EventHandler? Released;

    public void Start(DispatcherQueue dispatcher)
    {
        if (_timer is not null) return;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(35);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer sender, object arguments)
    {
        var shift = (NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0;
        var escape = (NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0;
        if (!shift)
            _dismissed = false;
        else if (escape || !preferences.Current.ExplorerShortcutEnabled)
            _dismissed = true;
        var foreground = NativeMethods.GetForegroundWindow();
        if (!shift || _dismissed || (_triggered && foreground != _sourceWindow))
        {
            if (_triggered) Released?.Invoke(this, EventArgs.Empty);
            if (_triggered && shift) _dismissed = true;
            _hold.Reset();
            _triggered = false;
            return;
        }
        if (_triggered) return;
        if (!NativeMethods.IsFileSurface(foreground)) { _hold.Reset(); return; }
        // A click or another modifier means Windows selection/dragging is in progress.
        if ((NativeMethods.GetAsyncKeyState(0x01) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(0x11) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0) { _hold.Reset(); return; }
        if (!_hold.IsRunning || _sourceWindow != foreground)
        {
            _sourceWindow = foreground;
            _hold.Restart();
        }
        if (_hold.ElapsedMilliseconds < preferences.Current.ShiftHoldMilliseconds) return;
        _triggered = true;
        var path = selection.GetSingleSelectedFile(foreground);
        if (path is not null) FileRequested?.Invoke(this, path);
    }

    public void Dispose()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }
}
