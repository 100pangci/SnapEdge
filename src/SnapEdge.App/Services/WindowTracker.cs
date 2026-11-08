using System.Windows.Threading;
using SnapEdge.App.Interop;

namespace SnapEdge.App.Services;

public sealed class WindowTracker : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private WindowSnapshot? _lastTarget;

    public WindowTracker()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _timer.Tick += (_, _) => SampleForeground();
        _timer.Start();
    }

    public nint GetCaptureTarget()
    {
        var current = TrySnapshot(NativeMethods.GetForegroundWindow());
        if (current is not null)
        {
            _lastTarget = current;
            return current.Value.Handle;
        }

        return _lastTarget is { } last && IsValidTarget(last) ? last.Handle : 0;
    }

    private void SampleForeground()
    {
        var foreground = TrySnapshot(NativeMethods.GetForegroundWindow());
        if (foreground is not null)
            _lastTarget = foreground;
        else if (_lastTarget is { } last && !IsValidTarget(last))
            _lastTarget = null;
    }

    private WindowSnapshot? TrySnapshot(nint hWnd)
    {
        if (hWnd == 0)
            return null;
        var root = NativeMethods.GetAncestor(hWnd, NativeMethods.GaRoot);
        if (root != 0)
            hWnd = root;
        if (!NativeMethods.IsWindow(hWnd) || !NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
            return null;

        var threadId = NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
        if (processId == 0 || processId == _ownProcessId || threadId == 0)
            return null;
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return null;
        if (!NativeMethods.GetWindowRect(hWnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
            return null;
        return new WindowSnapshot(hWnd, processId, threadId);
    }

    private bool IsValidTarget(WindowSnapshot snapshot)
    {
        if (!NativeMethods.IsWindow(snapshot.Handle) || !NativeMethods.IsWindowVisible(snapshot.Handle) || NativeMethods.IsIconic(snapshot.Handle))
            return false;
        var threadId = NativeMethods.GetWindowThreadProcessId(snapshot.Handle, out var processId);
        return processId == snapshot.ProcessId && threadId == snapshot.ThreadId && processId != _ownProcessId;
    }

    public void Dispose() => _timer.Stop();

    private readonly record struct WindowSnapshot(nint Handle, uint ProcessId, uint ThreadId);
}
